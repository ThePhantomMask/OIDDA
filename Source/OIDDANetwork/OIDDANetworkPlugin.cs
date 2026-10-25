using FlaxEngine;
using FlaxEngine.Networking;
using System;

namespace OIDDA.Network;

/// <summary>
/// Installs the network state provider into the ORS. Without a running network (offline) it reports authority, so the ORS
/// behaves exactly as in single player.
/// </summary>
public class OIDDANetworkPlugin : GamePlugin
{
    /// <summary> The state received from the server, for the network script to feed. Null before initialization. </summary>
    public static OIDDAStateCache Cache { get; private set; }

    private NetworkManagerMode _lastMode;

    public OIDDANetworkPlugin()
    {
        _description = new PluginDescription
        {
            Name = "OIDDA Network",
            Category = "Networking",
            Author = "Phantom Raptor Studio",
            RepositoryUrl = "https://github.com/ThePhantomMask/OIDDA",
            Description = "Replicates the OIDDA pacing state to clients and accepts their metrics.",
            Version = new Version(0, 9, 2550),
            IsAlpha = true,
            IsBeta = false
        };
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        Cache = new OIDDAStateCache(new FlaxNetworkEnvironment());
        ORS.StateProvider = Cache;
        _lastMode = NetworkManager.Mode;
        NetworkManager.StateChanged += OnNetworkStateChanged;
    }

    /// <inheritdoc />
    public override void Deinitialize()
    {
        NetworkManager.StateChanged -= OnNetworkStateChanged;

        if (ORS.StateProvider == Cache) ORS.StateProvider = null;
        Cache = null;

        base.Deinitialize();
    }

    // What was received belongs to one connection: drop it when the connection ends or the mode changes.
    private void OnNetworkStateChanged()
    {
        var mode = NetworkManager.Mode;
        if (mode != _lastMode || NetworkManager.State != NetworkConnectionState.Connected) Cache?.Clear();
        _lastMode = mode;
    }
}
