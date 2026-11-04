namespace OIDDA;

/// <summary>
/// Connects the ORS to a network layer. OIDDA only runs where there is authority (offline, server, host); a remote client
/// has no analysis of its own, so the ORS reads its state from a provider instead, and refuses to change it.
/// Register one with <see cref="ORS.StateProvider"/>. Without a provider everything works as in single player.
/// </summary>
public interface IOIDDAStateProvider
{
    /// <summary>
    /// True where OIDDA runs (offline, server, host). False on a remote client: the manager then stays idle and the
    /// ORS answers from this provider.
    /// </summary>
    bool IsAuthority { get; }

    /// <summary> The player this process plays as. On a client, "the default player" of the ORS means this one. </summary>
    uint LocalPlayerId { get; }

    /// <summary>
    /// The last state received for a player. False if that player's state is not available here (not received yet, or not
    /// shared with this client).
    /// </summary>
    bool TryGetPlayerState(uint playerId, out OIDDAStateSnapshot snapshot);

    /// <summary> The last state received for the group. False if there is no group or nothing received yet. </summary>
    bool TryGetGroupState(out OIDDAStateSnapshot snapshot);

    /// <summary>
    /// Sends a metric measured on the client to the server, if that is enabled. Returns false when it is not, and the
    /// ORS then ignores the call. The server must treat the value as untrusted.
    /// </summary>
    bool TryReportMetric(string name, object value);
}
