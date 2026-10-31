namespace OIDDA.Network;

/// <summary>
/// Ids the engine reserves.
/// </summary>
public static class OIDDANetworkIds
{
    /// <summary> Same value as NetworkManager.ServerClientId. The host's own client has this id too. </summary>
    public const uint Server = 0;
}

/// <summary>
/// Mirror of the engine's network manager modes, so the logic can be tested without the engine.
/// </summary>
public enum OIDDANetworkMode
{
    Offline,
    Server,
    Client,
    Host
}

/// <summary>
/// What the network logic needs to know about the network manager.
/// </summary>
public interface INetworkEnvironment
{
    OIDDANetworkMode Mode { get; }

    /// <summary> True once the connection to the server is established (clients only). </summary>
    bool IsConnected { get; }

    /// <summary> The client id of this process. On a client it is only valid once connected. </summary>
    uint LocalClientId { get; }
}

/// <summary>
/// Channel the periodic state messages travel on.
/// </summary>
public enum OIDDAStateChannel
{
    /// <summary> Every message arrives, in order. </summary>
    Reliable,

    /// <summary>
    /// Messages may be lost. Each one is a complete picture, so a lost one is replaced by the next. The first message to a client
    /// and any message too big for one packet still go on the reliable channel.
    /// </summary>
    Unreliable
}

/// <summary>
/// A metric a client is allowed to report, with the range its values are clamped to.
/// </summary>
public class AllowedClientMetric
{
    public string Name = "";

    public float Min = 0f;

    public float Max = 1f;
}
