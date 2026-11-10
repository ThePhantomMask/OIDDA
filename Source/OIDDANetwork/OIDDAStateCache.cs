using FlaxEngine;
using OIDDA.Data;
using System;
using System.Collections.Generic;

namespace OIDDA.Network;

/// <summary>
/// The <see cref="IOIDDAStateProvider"/> of a networked game. On a client it holds the last state the server sent and answers
/// the ORS from it. Where OIDDA runs (offline, server, host) it reports authority and the ORS ignores it.
/// </summary>
public sealed class OIDDAStateCache : IOIDDAStateProvider
{
    readonly INetworkEnvironment _env;
    // The last message applied, and the one being decoded: they trade places when a message is accepted.
    OIDDAStateFrame _current = new();
    OIDDAStateFrame _incoming = new();
    ushort _lastSequence;
    bool _hasSequence;

    public OIDDAStateCache(INetworkEnvironment env) => _env = env;

    /// <summary>
    /// Sends a metric to the server. Set by the network script when client metrics are enabled; null means they are not.
    /// </summary>
    public Func<string, float, bool> MetricReporter { get; set; }

    public bool IsAuthority => _env.Mode != OIDDANetworkMode.Client;

    // Only meaningful once connected: before that the engine's local client id is still 0, which would look like the host.
    public uint LocalPlayerId => _env.LocalClientId;

    public bool TryGetPlayerState(uint playerId, out OIDDAStateSnapshot snapshot)
    {
        if (IsAuthority || !_env.IsConnected)
        {
            snapshot = default;
            return false;
        }
        return _current.Players.TryGetValue(playerId, out snapshot);
    }

    /// <summary>
    /// The state of the local player's group. If the message did not say which group the local player is in, group 0.
    /// </summary>
    public bool TryGetGroupState(out OIDDAStateSnapshot snapshot)
    {
        if (IsAuthority || !_env.IsConnected)
        {
            snapshot = default;
            return false;
        }

        uint groupId = _current.PlayerGroups.TryGetValue(LocalPlayerId, out var own) ? own : OIDDAManager.DefaultGroupId;
        return _current.Groups.TryGetValue(groupId, out snapshot);
    }

    /// <summary>
    /// The last state received for a group. False if it was not in the last message.
    /// </summary>
    public bool TryGetGroupState(uint groupId, out OIDDAStateSnapshot snapshot)
    {
        if (IsAuthority || !_env.IsConnected)
        {
            snapshot = default;
            return false;
        }

        return _current.Groups.TryGetValue(groupId, out snapshot);
    }

    public bool TryReportMetric(string name, object value)
    {
        if (IsAuthority || !_env.IsConnected || MetricReporter == null) return false;
        if (!TryToFloat(value, out var number)) return false;

        return MetricReporter(name, number);
    }

    /// <summary>
    /// Takes a state message from the server. Only the server may send it, and a malformed message changes nothing.
    /// A message that is not newer than the last one applied (it arrived late, or twice) is dropped, so state never goes back in time.
    /// </summary>
    public bool Apply(byte[] payload, uint senderId)
    {
        if (IsAuthority || senderId != OIDDANetworkIds.Server) return false;
        if (!OIDDANetworkCodec.TryDecode(payload, _incoming)) return false;
        if (_hasSequence && !OIDDANetworkCodec.IsNewer(_incoming.Sequence, _lastSequence)) return false;

        // Every message is the complete picture of what this client may see, so it replaces the previous one.
        (_current, _incoming) = (_incoming, _current);
        _lastSequence = _current.Sequence;
        _hasSequence = true;
        return true;
    }

    /// <summary> Forgets everything received (on disconnection or when the mode changes). </summary>
    public void Clear()
    {
        _current.Clear();
        _hasSequence = false;
    }

    // Same conversion as the server uses to score a metric (see OIDDAValue); a double is also accepted here.
    private static bool TryToFloat(object value, out float number)
    {
        if (value is double d)
        {
            number = (float)d;
            return true;
        }

        return OIDDAValue.TryToFloat(value, out number);
    }
}
