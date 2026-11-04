using FlaxEngine;
using FlaxEngine.Networking;
using System.Text;

namespace OIDDA.Network;

/// <summary> What the demo should start. </summary>
public enum OIDDADemoMode
{
    /// <summary> Do nothing: start the network yourself. </summary>
    None,
    Server,
    Host,
    Client
}

/// <summary>
/// A small script to try OIDDA's multiplayer without writing any game code. Put it in a scene that has an OIDDAManager and an
/// OIDDANetworkSync, choose a mode, and run two instances of the game (one Host or Server, one Client).
/// It logs the state that the ORS reports, and F1 / F2 / F3 trigger events. Delete the Samples folder if you do not want it.
/// </summary>
public class OIDDANetworkDemo : Script
{
    [Tooltip("Start the network when the scene starts, in this mode.")]
    public OIDDADemoMode Mode = OIDDADemoMode.None;

    [Range(0.5f, 30f), Tooltip("Seconds between status lines in the log.")]
    public float LogInterval = 2f;

    [Range(1f, 100f), Tooltip("Intensity added by F1 (to the local player) and F2 (to the group).")]
    public float EventIntensity = 60f;

    private float _timer;
    private bool _started;

    /// <inheritdoc />
    public override void OnStart()
    {
        if (Mode == OIDDADemoMode.None) return;

        Start(Mode);
    }

    /// <inheritdoc />
    public override void OnDestroy()
    {
        if (_started) NetworkManager.Stop();
    }

    /// <inheritdoc />
    public override void OnUpdate()
    {
        HandleKeys();

        _timer += Time.UnscaledDeltaTime;
        if (_timer < LogInterval) return;
        _timer = 0f;

        Debug.Log(StatusLine());
    }

    internal bool Start(OIDDADemoMode mode)
    {
        // The engine's start functions return true when they fail.
        bool failed = mode switch
        {
            OIDDADemoMode.Server => NetworkManager.StartServer(),
            OIDDADemoMode.Host => NetworkManager.StartHost(),
            OIDDADemoMode.Client => NetworkManager.StartClient(),
            _ => false
        };

        if (failed) Debug.LogError($"[OIDDA Demo] Could not start the network as {mode}. Check the network settings (address, port, driver).");
        else _started = mode != OIDDADemoMode.None;

        return !failed;
    }

    internal void HandleKeys()
    {
        var ors = new ORS();

        if (Input.GetKeyDown(KeyboardKeys.F1))
        {
            ors.AddDirectorIntensity(EventIntensity, "demo F1");
            Debug.Log("[OIDDA Demo] F1: personal intensity event" + (ORS.IsRemoteClient ? " (ignored on a client: the server decides)" : ""));
        }

        if (Input.GetKeyDown(KeyboardKeys.F2))
        {
            ors.AddGroupIntensity(EventIntensity, "demo F2");
            Debug.Log("[OIDDA Demo] F2: group intensity event" + (ORS.IsRemoteClient ? " (ignored on a client: the server decides)" : ""));
        }

        if (Input.GetKeyDown(KeyboardKeys.F3))
        {
            ors.QuickSender("reaction", 0.5f);
            Debug.Log("[OIDDA Demo] F3: metric 'reaction' = 0.5" + (ORS.IsRemoteClient ? " (sent to the server only if client metrics are enabled)" : ""));
        }
    }

    internal static string StatusLine()
    {
        var ors = new ORS();
        var sb = new StringBuilder();

        sb.Append($"[OIDDA Demo] mode={NetworkManager.Mode} local={ors.LocalPlayerId} | me: {ors.CurrentState} int={ors.CurrentIntensity:F1} stress={ors.CurrentStress:F1} fatigue={ors.CurrentFatigue:F1}");

        if (ors.TryGetGroupState(out var group))
            sb.Append($" | group: {group.State} int={group.Intensity:F1} players={group.PlayerCount}");

        var manager = OIDDAPlugin.Instance?.Manager;
        if (!ORS.IsRemoteClient && manager) sb.Append($" | players on this machine: {manager.PlayerCount}");

        return sb.ToString();
    }
}
