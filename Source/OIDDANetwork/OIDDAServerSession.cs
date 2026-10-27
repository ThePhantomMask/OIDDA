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
    bool WriteMetric(uint playerId, string name, float value);
}

/// <summary>
/// One state message and the clients it is for.
/// </summary>
public readonly struct OutgoingState
{
    public readonly uint[] Targets;
    public readonly byte[] Payload;

    public OutgoingState(uint[] targets, byte[] payload)
    {
        Targets = targets;
        Payload = payload;
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
    Resumed
}

/// <summary> Counters of the reconnection logic, for debugging and tests. </summary>
public struct OIDDASessionStats
{
    public int Parked;
    public int Resumed;
    public int Expired;
    public int Evicted;
}

/// <summary>
/// Server-side logic: keeps OIDDA's players in step with the connected clients, builds what each client is sent,
/// handles metric reports and recognises returning players. The player id is the client id: the host's own player is 0
/// (the default player), remote clients start from 1, as the engine assigns them.
/// </summary>
public sealed class OIDDAServerSession
{
    struct Parked
    {
        public object Token;
        public float ExpiresAt;
    }

    readonly IOIDDAPlayerHost _host;
    readonly OIDDAMetricGate _gate;
    readonly float _graceSeconds;
    readonly int _maxParked;
    readonly HashSet<uint> _players = new();
    readonly HashSet<uint> _rejected = new();
    readonly HashSet<uint> _connected = new();
    readonly List<uint> _scratch = new();
    readonly List<uint> _idScratch = new();
    readonly List<string> _keyScratch = new();
    readonly List<OutgoingState> _messages = new();
    readonly List<KeyValuePair<uint, OIDDAStateSnapshot>> _all = new();
    readonly Dictionary<uint, uint[]> _targetCache = new();
    readonly Dictionary<uint, string> _keyByPlayer = new();
    readonly Dictionary<string, uint> _playerByKey = new();
    readonly Dictionary<string, Parked> _parked = new();
    uint[] _allTargets = System.Array.Empty<uint>();
    bool _allTargetsDirty = true;
    OIDDASessionStats _stats;

    /// <param name="gate">Decides which client metrics are accepted. Null refuses all of them.</param>
    /// <param name="reconnectGraceSeconds">How long a disconnected player's state is kept for them to come back. 0 turns reconnection off.</param>
    /// <param name="maxParked">Most disconnected players kept at once; beyond it the oldest is dropped.</param>
    public OIDDAServerSession(IOIDDAPlayerHost host, OIDDAMetricGate gate = null, float reconnectGraceSeconds = 0f, int maxParked = 64)
    {
        _host = host;
        _gate = gate;
        _graceSeconds = reconnectGraceSeconds;
        _maxParked = maxParked;
    }

    /// <summary> Remote clients that have a player. </summary>
    public IReadOnlyCollection<uint> Players => _players;

    /// <summary> Clients that were refused because the manager's MaxPlayers was reached. </summary>
    public IReadOnlyCollection<uint> Rejected => _rejected;

    /// <summary> Disconnected players whose state is being kept. </summary>
    public int ParkedCount => _parked.Count;

    public OIDDASessionStats Stats => _stats;

    /// <summary>
    /// Registers a player for each newly connected remote client and removes the ones that left. A player who sent a key keeps
    /// their state for the grace period instead of losing it. Safe to call every tick.
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

        _connected.Clear();
        for (int i = 0; i < connectedRemoteIds.Count; i++)
        {
            var id = connectedRemoteIds[i];
            if (id == OIDDANetworkIds.Server) continue;

            _connected.Add(id);
            if (_players.Contains(id) || _rejected.Contains(id)) continue;

            // A refused client is not retried every tick (the manager warns each time): only after someone leaves.
            if (_host.RegisterPlayer(id))
            {
                _players.Add(id);
                _allTargetsDirty = true;
            }
            else
            {
                _rejected.Add(id);
            }
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
    void Leave(uint id, float now)
    {
        if (_keyByPlayer.TryGetValue(id, out var key))
        {
            _keyByPlayer.Remove(id);
            _playerByKey.Remove(key);

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
        if (_graceSeconds <= 0f || !OIDDAPlayerKey.IsValid(key) || !_players.Contains(senderId)) return IdentifyResult.Rejected;
        if (_keyByPlayer.ContainsKey(senderId) || _playerByKey.ContainsKey(key)) return IdentifyResult.Rejected;

        var result = IdentifyResult.Identified;

        if (_parked.TryGetValue(key, out var parked))
        {
            _parked.Remove(key);

            if (now < parked.ExpiresAt && _host.ResumePlayer(senderId, parked.Token))
            {
                _stats.Resumed++;
                result = IdentifyResult.Resumed;
            }
        }

        _keyByPlayer[senderId] = key;
        _playerByKey[key] = senderId;
        return result;
    }

    /// <summary>
    /// Builds what to send. By default each client receives only its own player and the group; with
    /// <paramref name="replicateToAll"/> everyone receives every player (and the host's own player, when it is a real player).
    /// The returned list and its target arrays are reused by the next call: send them before building again.
    /// </summary>
    public List<OutgoingState> BuildMessages(bool replicateToAll, bool includeHostPlayer)
    {
        _messages.Clear();
        if (_players.Count == 0) return _messages;

        bool hasGroup = _host.TryGetGroupState(out var groupState);
        OIDDAStateSnapshot? group = hasGroup ? groupState : null;

        if (replicateToAll)
        {
            _all.Clear();
            foreach (var id in _players)
                if (_host.TryGetPlayerState(id, out var snapshot)) _all.Add(new KeyValuePair<uint, OIDDAStateSnapshot>(id, snapshot));

            if (includeHostPlayer && _host.TryGetPlayerState(OIDDANetworkIds.Server, out var hostState))
                _all.Add(new KeyValuePair<uint, OIDDAStateSnapshot>(OIDDANetworkIds.Server, hostState));

            if (_allTargetsDirty)
            {
                _allTargets = new uint[_players.Count];
                _players.CopyTo(_allTargets);
                _allTargetsDirty = false;
            }

            _messages.Add(new OutgoingState(_allTargets, OIDDANetworkCodec.Encode(_all, group)));
            return _messages;
        }

        foreach (var id in _players)
        {
            byte[] payload;
            if (_host.TryGetPlayerState(id, out var own)) payload = OIDDANetworkCodec.EncodeOne(id, own, group);
            else if (hasGroup) payload = OIDDANetworkCodec.EncodeGroupOnly(groupState);
            else continue;

            if (!_targetCache.TryGetValue(id, out var target))
            {
                target = new[] { id };
                _targetCache[id] = target;
            }

            _messages.Add(new OutgoingState(target, payload));
        }
        return _messages;
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
