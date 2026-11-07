using System.Collections.Generic;

namespace OIDDA.Network;

/// <summary>
/// What the server-side logic needs from OIDDA. <see cref="ManagerPlayerHost"/> implements it over the real manager.
/// </summary>
public interface IOIDDAPlayerHost
{
    bool RegisterPlayer(uint playerId);
    bool UnregisterPlayer(uint playerId);

    /// <summary> Takes a player out of play keeping their state. Returns an opaque token, or null if that is not possible. </summary>
    object ParkPlayer(uint playerId);

    /// <summary> Puts a parked player back in play under a (new) id. </summary>
    bool ResumePlayer(uint playerId, object parked);

    bool TryGetPlayerState(uint playerId, out OIDDAStateSnapshot snapshot);
    bool TryGetGroupState(out OIDDAStateSnapshot snapshot);
    void SetDefaultPlayerIsReal(bool value);

    /// <summary> The group a player is in. By default 0: a host without groups puts everyone in group 0. </summary>
    uint GetPlayerGroup(uint playerId) => 0;

    /// <summary> Adds the state of every group, with its id, to the list. By default only group 0, if there is one. </summary>
    void GetGroupStates(List<KeyValuePair<uint, OIDDAStateSnapshot>> into)
    {
        if (TryGetGroupState(out var snapshot)) into.Add(new KeyValuePair<uint, OIDDAStateSnapshot>(0, snapshot));
    }

    bool WriteMetric(uint playerId, string name, float value);

    /// <summary> The profile of a player (see <c>OIDDAProfile</c>) to keep between sessions, or null if there is none. </summary>
    byte[] CaptureProfile(uint playerId) => null;

    /// <summary> Puts a saved profile into a player. False if the profile is not valid or the player is unknown. </summary>
    bool RestoreProfile(uint playerId, byte[] profile) => false;
}

/// <summary>
/// One state message and the clients it is for.
/// </summary>
public readonly struct OutgoingState
{
    public readonly uint[] Targets;
    public readonly byte[] Payload;

    /// <summary> True if the message must go on the reliable channel. </summary>
    public readonly bool Reliable;

    public OutgoingState(uint[] targets, byte[] payload, bool reliable)
    {
        Targets = targets;
        Payload = payload;
        Reliable = reliable;
    }
}

/// <summary> What happened to a player key a client sent. </summary>
public enum IdentifyResult
{
    /// <summary> Ignored: invalid, duplicate, unknown sender, or reconnection is off. </summary>
    Rejected,

    /// <summary> The key now belongs to this player; nothing was waiting for it. </summary>
    Identified,

    /// <summary> A returning player: their previous state was restored. </summary>
    Resumed,

    /// <summary> A player seen in an earlier session: their saved profile was loaded. </summary>
    Restored
}

/// <summary> Counters of the reconnection logic, for debugging and tests. </summary>
public struct OIDDASessionStats
{
    public int Parked;
    public int Resumed;
    public int Expired;
    public int Evicted;

    /// <summary> Profiles saved. </summary>
    public int Saved;

    /// <summary> Profiles loaded for returning players. </summary>
    public int Restored;
}

/// <summary>
/// Server-side logic: keeps OIDDA's players in step with the connected clients, builds what each client is sent,
/// handles metric reports and recognises returning players. The player id is the client id: the host's own player is 0
/// (the default player), remote clients start from 1, as the engine assigns them.
/// </summary>
public sealed class OIDDAServerSession
{
    /// <summary>
    /// Biggest message (in bytes) sent on an unreliable channel. A bigger one is split into parts by the engine and losing any part
    /// loses the whole message, so it goes on the reliable channel instead.
    /// </summary>
    public const int MaxUnreliableBytes = 1000;

    struct Parked
    {
        public object Token;
        public float ExpiresAt;
    }

    readonly IOIDDAPlayerHost _host;
    readonly OIDDAMetricGate _gate;
    readonly float _graceSeconds;
    readonly int _maxParked;
    readonly bool _saveProfiles;
    readonly float _autosaveSeconds;
    readonly List<uint> _saveQueue = new();
    float _nextAutosave = -1f;
    int _saveCursor;
    readonly HashSet<uint> _players = new();
    readonly HashSet<uint> _needReliable = new();
    readonly HashSet<uint> _rejected = new();
    readonly HashSet<uint> _connected = new();
    readonly List<uint> _scratch = new();
    readonly List<uint> _idScratch = new();
    readonly List<string> _keyScratch = new();
    readonly List<OutgoingState> _messages = new();
    readonly List<KeyValuePair<uint, OIDDAStateSnapshot>> _all = new();
    readonly List<(uint Id, uint GroupId, OIDDAStateSnapshot State)> _entries = new();
    readonly List<KeyValuePair<uint, OIDDAStateSnapshot>> _groupStates = new();
    readonly Dictionary<uint, uint[]> _targetCache = new();
    readonly Dictionary<uint, string> _keyByPlayer = new();
    readonly Dictionary<string, uint> _playerByKey = new();
    readonly Dictionary<string, Parked> _parked = new();
    uint[] _allTargets = System.Array.Empty<uint>();
    bool _allTargetsDirty = true;
    OIDDASessionStats _stats;
    ushort _sequence;

    /// <summary>
    /// Initialize the OIDDA Server session.
    /// </summary>
    /// <param name="host">Server</param>
    /// <param name="gate">Decides which client metrics are accepted. Null refuses all of them.</param>
    /// <param name="reconnectGraceSeconds">How long a disconnected player's state is kept for them to come back. 0 turns reconnection off.</param>
    /// <param name="maxParked">Most disconnected players kept at once; beyond it the oldest is dropped.</param>
    /// <param name="saveProfiles">Keep the profile of each identified player between sessions (see <see cref="OIDDAProfileStore"/>).</param>
    /// <param name="profileAutosaveSeconds">With saveProfiles, how often the profiles of the connected players are saved. 0: only when they leave and when the server stops.</param>
    public OIDDAServerSession(IOIDDAPlayerHost host, OIDDAMetricGate gate = null, float reconnectGraceSeconds = 0f, int maxParked = 64, bool saveProfiles = false, float profileAutosaveSeconds = 0f)
    {
        _host = host;
        _gate = gate;
        _graceSeconds = reconnectGraceSeconds;
        _maxParked = maxParked;
        _saveProfiles = saveProfiles;
        _autosaveSeconds = profileAutosaveSeconds;
    }

    /// <summary> Most profiles written in one call of <see cref="Reconcile(List{uint}, bool, float)"/>, so saving many players never stalls a frame. </summary>
    public const int MaxAutosavesPerTick = 4;

    /// <summary> 
    /// Remote clients that have a player. 
    /// </summary>
    public IReadOnlyCollection<uint> Players => _players;

    /// <summary> 
    /// Clients that were refused because the manager's MaxPlayers was reached. 
    /// </summary>
    public IReadOnlyCollection<uint> Rejected => _rejected;

    /// <summary> 
    /// Disconnected players whose state is being kept. 
    /// </summary>
    public int ParkedCount => _parked.Count;

    public OIDDASessionStats Stats => _stats;

    /// <summary>
    /// Registers a player for each newly connected remote client and removes the ones that left. 
    /// A player who sent a key keeps their state for the grace period instead of losing it. 
    /// Safe to call every tick.
    /// </summary>
    /// <param name="connectedRemoteIds">Ids of the connected remote clients. The host's own client (id 0) is ignored.</param>
    /// <param name="defaultPlayerIsReal">False on a dedicated server.</param>
    /// <param name="now">Current time in seconds, from a clock that always moves forward.</param>
    public void Reconcile(IEnumerable<uint> connectedRemoteIds, bool defaultPlayerIsReal, float now = 0f)
    {
        _idScratch.Clear();
        _idScratch.AddRange(connectedRemoteIds);
        Reconcile(_idScratch, defaultPlayerIsReal, now);
    }

    /// <summary>
    /// Same as the other overload, for callers that already have a list (no allocation).
    /// </summary>
    public void Reconcile(List<uint> connectedRemoteIds, bool defaultPlayerIsReal, float now = 0f)
    {
        _host.SetDefaultPlayerIsReal(defaultPlayerIsReal);
        PurgeExpired(now);
        Autosave(now);

        _connected.Clear();
        for (int i = 0; i < connectedRemoteIds.Count; i++)
        {
            var id = connectedRemoteIds[i];
            if (id == OIDDANetworkIds.Server) continue;

            _connected.Add(id);
            if (_players.Contains(id) || _rejected.Contains(id)) continue;

            // A refused client is not retried every tick (the manager warns each time): only after someone leaves.
            if (!_host.RegisterPlayer(id))
            {
                _rejected.Add(id);
                continue;
            }

            _players.Add(id);
            _needReliable.Add(id);
            _allTargetsDirty = true;
        }

        _scratch.Clear();
        foreach (var id in _players)
            if (!_connected.Contains(id)) _scratch.Add(id);

        bool freedSlot = _scratch.Count > 0;
        for (int i = 0; i < _scratch.Count; i++)
        {
            var id = _scratch[i];
            Leave(id, now);
            _players.Remove(id);
            _needReliable.Remove(id);
            _targetCache.Remove(id);
            _gate?.Forget(id);
            _allTargetsDirty = true;
        }

        if (_rejected.Count > 0)
        {
            _scratch.Clear();
            foreach (var id in _rejected)
                if (!_connected.Contains(id)) _scratch.Add(id);
            for (int i = 0; i < _scratch.Count; i++) _rejected.Remove(_scratch[i]);

            if (freedSlot) _rejected.Clear();
        }
    }

    // A player who identified themselves can come back: keep their state aside. Anyone else is simply removed.
    void SaveProfile(uint id, string key)
    {
        var profile = _host.CaptureProfile(id);
        if (profile != null && OIDDAProfileStore.Save(key, profile)) _stats.Saved++;
    }

    // Saves the connected players one after another: a few per call, then the rest in the next calls.
    void Autosave(float now)
    {
        if (!_saveProfiles || _autosaveSeconds <= 0f) return;

        if (_saveCursor >= _saveQueue.Count)
        {
            if (_nextAutosave < 0f)
            {
                _nextAutosave = now + _autosaveSeconds;
                return;
            }

            if (now < _nextAutosave) return;

            _nextAutosave = now + _autosaveSeconds;
            _saveQueue.Clear();
            foreach (var id in _keyByPlayer.Keys) _saveQueue.Add(id);
            _saveCursor = 0;
        }

        for (int n = 0; n < MaxAutosavesPerTick && _saveCursor < _saveQueue.Count; n++, _saveCursor++)
        {
            var id = _saveQueue[_saveCursor];
            if (_keyByPlayer.TryGetValue(id, out var key)) SaveProfile(id, key);
        }
    }

    /// <summary> Saves the profile of every identified player now (when the server stops). Does nothing unless profiles are on. </summary>
    public void FlushProfiles()
    {
        if (!_saveProfiles) return;

        foreach (var pair in _keyByPlayer) SaveProfile(pair.Key, pair.Value);
    }

    void Leave(uint id, float now)
    {
        if (_keyByPlayer.TryGetValue(id, out var key))
        {
            _keyByPlayer.Remove(id);
            _playerByKey.Remove(key);

            if (_saveProfiles) SaveProfile(id, key);

            if (_graceSeconds > 0f && _maxParked > 0)
            {
                var token = _host.ParkPlayer(id);
                if (token != null)
                {
                    EvictOldestIfFull();
                    _parked[key] = new Parked { Token = token, ExpiresAt = now + _graceSeconds };
                    _stats.Parked++;
                    return;
                }
            }
        }

        _host.UnregisterPlayer(id);
    }

    void PurgeExpired(float now)
    {
        if (_parked.Count == 0) return;

        _keyScratch.Clear();
        foreach (var kv in _parked)
            if (now >= kv.Value.ExpiresAt) _keyScratch.Add(kv.Key);

        for (int i = 0; i < _keyScratch.Count; i++)
        {
            _parked.Remove(_keyScratch[i]);
            _stats.Expired++;
        }
    }

    void EvictOldestIfFull()
    {
        while (_parked.Count >= _maxParked && _parked.Count > 0)
        {
            string oldest = null;
            float earliest = float.MaxValue;
            foreach (var kv in _parked)
            {
                if (kv.Value.ExpiresAt >= earliest) continue;
                earliest = kv.Value.ExpiresAt;
                oldest = kv.Key;
            }

            _parked.Remove(oldest);
            _stats.Evicted++;
        }
    }

    /// <summary>
    /// A client tells the server its player key. If a disconnected player was waiting for that key, their state is restored.
    /// The sender id comes from the network layer. One key per connection, and a key already used by a connected player is refused.
    /// </summary>
    public IdentifyResult HandleIdentify(uint senderId, string key, float now)
    {
        if ((_graceSeconds <= 0f && !_saveProfiles) || !OIDDAPlayerKey.IsValid(key) || !_players.Contains(senderId)) return IdentifyResult.Rejected;
        if (_keyByPlayer.ContainsKey(senderId) || _playerByKey.ContainsKey(key)) return IdentifyResult.Rejected;

        var result = IdentifyResult.Identified;

        if (_parked.TryGetValue(key, out var parked))
        {
            _parked.Remove(key);

            if (now < parked.ExpiresAt && _host.ResumePlayer(senderId, parked.Token))
            {
                _stats.Resumed++;
                result = IdentifyResult.Resumed;
                _needReliable.Add(senderId); // their state jumped back: make sure they get it
            }
        }

        // Nobody was waiting in memory: maybe the player was here in an earlier session.
        if (result == IdentifyResult.Identified && _saveProfiles && OIDDAProfileStore.TryLoad(key, out var profile) && _host.RestoreProfile(senderId, profile))
        {
            _stats.Restored++;
            result = IdentifyResult.Restored;
            _needReliable.Add(senderId);
        }

        _keyByPlayer[senderId] = key;
        _playerByKey[key] = senderId;
        return result;
    }

    /// <summary>
    /// Builds what to send. By default each client receives only its own player and the group; with
    /// <paramref name="replicateToAll"/> everyone receives every player (and the host's own player, when it is a real player).
    /// The returned list and its target arrays are reused by the next call: send them before building again.
    /// Every call numbers its messages with the next sequence number. With <paramref name="allowUnreliable"/> a message may go on
    /// an unreliable channel, except the first one to a client (new or resumed) and any bigger than <see cref="MaxUnreliableBytes"/>:
    /// those are marked reliable.
    /// With <paramref name="format"/> <see cref="OIDDAStateFormat.V3"/> each message also tells the group of each player and carries
    /// the state of every group; the default (V2) is the format of earlier versions, which carries group 0 only.
    /// </summary>
    public List<OutgoingState> BuildMessages(bool replicateToAll, bool includeHostPlayer, bool allowUnreliable = false, OIDDAStateFormat format = OIDDAStateFormat.V2)
    {
        _messages.Clear();
        if (_players.Count == 0) return _messages;

        unchecked { _sequence++; }

        if (format == OIDDAStateFormat.V3) return BuildMessagesV3(replicateToAll, includeHostPlayer, allowUnreliable);

        bool hasGroup = _host.TryGetGroupState(out var groupState);
        OIDDAStateSnapshot? group = hasGroup ? groupState : null;

        if (replicateToAll)
        {
            _all.Clear();
            foreach (var id in _players)
                if (_host.TryGetPlayerState(id, out var snapshot)) _all.Add(new KeyValuePair<uint, OIDDAStateSnapshot>(id, snapshot));

            if (includeHostPlayer && _host.TryGetPlayerState(OIDDANetworkIds.Server, out var hostState))
                _all.Add(new KeyValuePair<uint, OIDDAStateSnapshot>(OIDDANetworkIds.Server, hostState));

            AddMessage(AllTargets(), OIDDANetworkCodec.Encode(_all, group, _sequence), allowUnreliable);
            return _messages;
        }

        foreach (var id in _players)
        {
            byte[] payload;
            if (_host.TryGetPlayerState(id, out var own)) payload = OIDDANetworkCodec.EncodeOne(id, own, group, _sequence);
            else if (hasGroup) payload = OIDDANetworkCodec.EncodeGroupOnly(groupState, _sequence);
            else continue;

            AddMessage(TargetOf(id), payload, allowUnreliable);
        }
        return _messages;
    }

    // Format 3: each player carries its group id, and every group's state is sent (to everyone, whichever players they get).
    List<OutgoingState> BuildMessagesV3(bool replicateToAll, bool includeHostPlayer, bool allowUnreliable)
    {
        _groupStates.Clear();
        _host.GetGroupStates(_groupStates);

        if (replicateToAll)
        {
            _entries.Clear();
            foreach (var id in _players)
                if (_host.TryGetPlayerState(id, out var snapshot)) _entries.Add((id, _host.GetPlayerGroup(id), snapshot));

            if (includeHostPlayer && _host.TryGetPlayerState(OIDDANetworkIds.Server, out var hostState))
                _entries.Add((OIDDANetworkIds.Server, _host.GetPlayerGroup(OIDDANetworkIds.Server), hostState));

            AddMessage(AllTargets(), OIDDANetworkCodec.EncodeV3(_entries, _groupStates, _sequence), allowUnreliable);
            return _messages;
        }

        foreach (var id in _players)
        {
            _entries.Clear();
            if (_host.TryGetPlayerState(id, out var own)) _entries.Add((id, _host.GetPlayerGroup(id), own));
            else if (_groupStates.Count == 0) continue;

            AddMessage(TargetOf(id), OIDDANetworkCodec.EncodeV3(_entries, _groupStates, _sequence), allowUnreliable);
        }
        return _messages;
    }

    uint[] AllTargets()
    {
        if (_allTargetsDirty)
        {
            _allTargets = new uint[_players.Count];
            _players.CopyTo(_allTargets);
            _allTargetsDirty = false;
        }
        return _allTargets;
    }

    uint[] TargetOf(uint id)
    {
        if (!_targetCache.TryGetValue(id, out var target))
        {
            target = new[] { id };
            _targetCache[id] = target;
        }
        return target;
    }

    void AddMessage(uint[] targets, byte[] payload, bool allowUnreliable)
    {
        bool reliable = !allowUnreliable || payload.Length > MaxUnreliableBytes || AnyNeedsReliable(targets);
        if (reliable && _needReliable.Count > 0)
            for (int i = 0; i < targets.Length; i++) _needReliable.Remove(targets[i]);

        _messages.Add(new OutgoingState(targets, payload, reliable));
    }

    bool AnyNeedsReliable(uint[] targets)
    {
        if (_needReliable.Count == 0) return false;

        for (int i = 0; i < targets.Length; i++)
            if (_needReliable.Contains(targets[i])) return true;
        return false;
    }

    /// <summary>
    /// Handles a metric a client reported. The sender id comes from the network layer, never from the message, so a client
    /// can only report for itself.
    /// </summary>
    public bool HandleReport(uint senderId, string name, float value, float now)
    {
        if (_gate == null || !_players.Contains(senderId)) return false;
        if (!_gate.TryAccept(senderId, name, value, now, out var clamped)) return false;

        return _host.WriteMetric(senderId, name, clamped);
    }
}
