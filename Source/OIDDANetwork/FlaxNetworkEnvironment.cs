using FlaxEngine.Networking;

namespace OIDDA.Network;

/// <summary>
/// <see cref="INetworkEnvironment"/> over the engine's <see cref="NetworkManager"/>.
/// </summary>
public sealed class FlaxNetworkEnvironment : INetworkEnvironment
{
    public OIDDANetworkMode Mode => NetworkManager.Mode switch
    {
        NetworkManagerMode.Server => OIDDANetworkMode.Server,
        NetworkManagerMode.Client => OIDDANetworkMode.Client,
        NetworkManagerMode.Host => OIDDANetworkMode.Host,
        _ => OIDDANetworkMode.Offline
    };

    public bool IsConnected => NetworkManager.State == NetworkConnectionState.Connected;

    public uint LocalClientId => NetworkManager.LocalClientId;
}
