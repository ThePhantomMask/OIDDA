using System.Collections.Generic;

namespace OIDDA.Network;

/// <summary>
/// What the server-side logic needs from OIDDA. <see cref="ManagerPlayerHost"/> implements it over the real manager.
/// </summary>
public interface IOIDDAPlayerHost
{
    bool RegisterPlayer(uint playerId);
    bool UnregisterPlayer(uint playerId);
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

/// <summary>
/// Server-side logic: keeps OIDDA's players in step with the connected clients, builds what each client is sent,
/// and handles metric reports. The player id is the client id: the host's own player is 0 (the default player),
/// remote clients start from 1, as the engine assigns them.
/// </summary>
public sealed class OIDDAServerSession
{
    readonly IOIDDAPlayerHost _host;
    readonly OIDDAMetricGate _gate;
    readonly HashSet<uint> _players = new();
    readonly HashSet<uint> _rejected = new();
    readonly HashSet<uint> _connected = new();
    readonly List<uint> _scratch = new();

    /// <param name="gate">Decides which client metrics are accepted. Null refuses all of them.</param>
    public OIDDAServerSession(IOIDDAPlayerHost host, OIDDAMetricGate gate = null)
    {
        _host = host;
        _gate = gate;
    }

    /// <summary> Remote clients that have a player. </summary>
    public IReadOnlyCollection<uint> Players => _players;

    /// <summary> Clients that were refused because the manager's MaxPlayers was reached. </summary>
    public IReadOnlyCollection<uint> Rejected => _rejected;

    /// <summary>
    /// Registers a player for each newly connected remote client and removes the ones that left. Safe to call every tick.
    /// </summary>
    /// <param name="connectedRemoteIds">Ids of the connected remote clients. The host's own client (id 0) is ignored.</param>
    /// <param name="defaultPlayerIsReal">False on a dedicated server.</param>
    public void Reconcile(IEnumerable<uint> connectedRemoteIds, bool defaultPlayerIsReal)
    {
        _host.SetDefaultPlayerIsReal(defaultPlayerIsReal);

        _connected.Clear();
        foreach (var id in connectedRemoteIds)
        {
            if (id == OIDDANetworkIds.Server) continue;

            _connected.Add(id);
            if (_players.Contains(id) || _rejected.Contains(id)) continue;

            // A refused client is not retried every tick (the manager warns each time): only after someone leaves.
            if (_host.RegisterPlayer(id)) _players.Add(id);
            else _rejected.Add(id);
        }

        _scratch.Clear();
        foreach (var id in _players)
            if (!_connected.Contains(id)) _scratch.Add(id);

        bool freedSlot = _scratch.Count > 0;
        foreach (var id in _scratch)
        {
            _host.UnregisterPlayer(id);
            _players.Remove(id);
            _gate?.Forget(id);
        }

        _rejected.RemoveWhere(id => !_connected.Contains(id));
        if (freedSlot) _rejected.Clear();
    }

    /// <summary>
    /// Builds what to send. By default each client receives only its own player and the group; with
    /// <paramref name="replicateToAll"/> everyone receives every player (and the host's own player, when it is a real player).
    /// </summary>
    public List<OutgoingState> BuildMessages(bool replicateToAll, bool includeHostPlayer)
    {
        var messages = new List<OutgoingState>();
        if (_players.Count == 0) return messages;

        bool hasGroup = _host.TryGetGroupState(out var groupState);
        OIDDAStateSnapshot? group = hasGroup ? groupState : null;

        var states = new Dictionary<uint, OIDDAStateSnapshot>();
        foreach (var id in _players)
            if (_host.TryGetPlayerState(id, out var snapshot)) states[id] = snapshot;

        if (replicateToAll)
        {
            var all = new List<KeyValuePair<uint, OIDDAStateSnapshot>>(states);
            if (includeHostPlayer && _host.TryGetPlayerState(OIDDANetworkIds.Server, out var hostState))
                all.Add(new KeyValuePair<uint, OIDDAStateSnapshot>(OIDDANetworkIds.Server, hostState));

            var targets = new uint[_players.Count];
            _players.CopyTo(targets);
            messages.Add(new OutgoingState(targets, OIDDANetworkCodec.Encode(all, group)));
            return messages;
        }

        foreach (var id in _players)
        {
            var own = new List<KeyValuePair<uint, OIDDAStateSnapshot>>(1);
            if (states.TryGetValue(id, out var snapshot)) own.Add(new KeyValuePair<uint, OIDDAStateSnapshot>(id, snapshot));
            if (own.Count == 0 && !hasGroup) continue;

            messages.Add(new OutgoingState(new[] { id }, OIDDANetworkCodec.Encode(own, group)));
        }
        return messages;
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
