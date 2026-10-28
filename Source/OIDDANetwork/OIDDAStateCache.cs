using FlaxEngine;
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
    readonly Dictionary<uint, OIDDAStateSnapshot> _players = new();
    readonly Dictionary<uint, OIDDAStateSnapshot> _incoming = new();
    OIDDAStateSnapshot? _group;

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
        return _players.TryGetValue(playerId, out snapshot);
    }

    public bool TryGetGroupState(out OIDDAStateSnapshot snapshot)
    {
        if (IsAuthority || !_env.IsConnected || !_group.HasValue)
        {
            snapshot = default;
            return false;
        }

        snapshot = _group.Value;
        return true;
    }

    public bool TryReportMetric(string name, object value)
    {
        if (IsAuthority || !_env.IsConnected || MetricReporter == null) return false;
        if (!TryToFloat(value, out var number)) return false;

        return MetricReporter(name, number);
    }

    /// <summary>
    /// Takes a state message from the server. Only the server may send it, and a malformed message changes nothing.
    /// </summary>
    public bool Apply(byte[] payload, uint senderId)
    {
        if (IsAuthority || senderId != OIDDANetworkIds.Server) return false;
        if (!OIDDANetworkCodec.TryDecode(payload, _incoming, out var group)) return false;

        // Every message is the complete picture of what this client may see, so it replaces the previous one.
        _players.Clear();
        foreach (var kv in _incoming) _players[kv.Key] = kv.Value;
        _group = group;
        return true;
    }

    /// <summary> Forgets everything received (on disconnection or when the mode changes). </summary>
    public void Clear()
    {
        _players.Clear();
        _group = null;
    }

    private static bool TryToFloat(object value, out float number)
    {
        switch (value)
        {
            case float f: number = f; return true;
            case double d: number = (float)d; return true;
            case int i: number = i; return true;
            case bool b: number = b ? 1f : 0f; return true;
            case Color c: number = c.ValuesSum; return true;
            case Vector2 v2: number = v2.Length; return true;
            case Vector3 v3: number = v3.Length; return true;
            case Vector4 v4: number = v4.Length; return true;
            case Quaternion q: number = q.Length; return true;
            case Transform t: number = t.Translation.ValuesSum; return true;
            case Matrix m: number = m.TranslationVector.ValuesSum; return true;
            default: number = 0f; return false;
        }
    }
}
