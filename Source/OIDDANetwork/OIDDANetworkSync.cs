using FlaxEngine;
using FlaxEngine.Networking;
using System;
using System.Collections.Generic;

namespace OIDDA.Network;

/// <summary>
/// Place one in the scene (on the same actor as the OIDDAManager works), on every peer. On the server or host it keeps OIDDA's
/// players in step with the connected clients and sends each client the state it may see; on a client it receives that state
/// and, if enabled, sends metrics measured on the client.
/// </summary>
public class OIDDANetworkSync : Script
{
    [EditorDisplay("State"), Tooltip("Off: each client only receives its own player's state (and the group's). On: every client receives every player's state, for example to show teammates in the UI.")]
    public bool ReplicateStateToAll = false;

    [EditorDisplay("State"), Range(0.02f, 2f), Tooltip("Seconds between state messages to the clients.")]
    public float StateSendInterval = 0.1f;

    [EditorDisplay("Client metrics"), Tooltip("Lets clients report metrics that only exist on the client (for example a reaction time). Off by default: the server is the only source of metrics.")]
    public bool AllowClientMetrics = false;

    [EditorDisplay("Client metrics"), Range(1, 100), Tooltip("Reports per second accepted from each client.")]
    public int MaxReportsPerSecond = 10;

    [EditorDisplay("Client metrics"), Collection(Display = CollectionAttribute.DisplayType.Header), Tooltip("The only metrics clients may report. Values are clamped to the range given here.")]
    public List<AllowedClientMetric> AllowedClientMetrics = new();

    // Test seams: where state and reports go when they are not sent through the network.
    internal Action<byte[], uint[]> StateSender;
    internal Func<string, float, bool> ReportSender;

    private OIDDAServerSession _session;
    private ManagerPlayerHost _host;
    private OIDDAMetricGate _gate;
    private Func<string, float, bool> _reporter;
    private float _timer;
    private bool _logged;

    /// <inheritdoc />
    public override void OnEnable()
    {
        NetworkReplicator.AddObject(this);

        _gate = AllowClientMetrics ? new OIDDAMetricGate(AllowedClientMetrics, MaxReportsPerSecond) : null;

        NetworkManager.ClientConnected += OnClientConnected;
        NetworkManager.ClientDisconnected += OnClientDisconnected;

        var cache = OIDDANetworkPlugin.Cache;
        if (cache != null && AllowClientMetrics)
        {
            _reporter = SendMetric;
            cache.MetricReporter = _reporter;
        }
    }

    /// <inheritdoc />
    public override void OnDisable()
    {
        NetworkManager.ClientConnected -= OnClientConnected;
        NetworkManager.ClientDisconnected -= OnClientDisconnected;

        var cache = OIDDANetworkPlugin.Cache;
        if (cache != null && _reporter != null && cache.MetricReporter == _reporter) cache.MetricReporter = null;
        _reporter = null;

        NetworkReplicator.RemoveObject(this);

        _session = null;
        _host = null;
    }

    /// <inheritdoc />
    public override void OnUpdate()
    {
        var mode = NetworkManager.Mode;
        if (mode != NetworkManagerMode.Server && mode != NetworkManagerMode.Host) return;

        ServerTick(Time.UnscaledDeltaTime);
    }

    internal void ServerTick(float deltaTime)
    {
        var manager = OIDDAPlugin.Instance?.Manager;
        if (!manager) return;

        // A new scene brings a new manager: start over with it.
        if (_host == null || _host.Manager != manager)
        {
            _host = new ManagerPlayerHost(manager);
            _session = new OIDDAServerSession(_host, _gate);
            _timer = 0f;
        }

        _timer += deltaTime;
        if (_timer < StateSendInterval) return;
        _timer = 0f;

        bool defaultPlayerIsReal = NetworkManager.Mode != NetworkManagerMode.Server;

        _session.Reconcile(ConnectedRemoteIds(), defaultPlayerIsReal);

        if (manager.DebugMode && !_logged)
        {
            _logged = true;
            Debug.Log($"[OIDDA Network] Mode={NetworkManager.Mode}, LocalClientId={NetworkManager.LocalClientId}, remote clients=[{string.Join(", ", _session.Players)}]");
        }

        foreach (var message in _session.BuildMessages(ReplicateStateToAll, defaultPlayerIsReal))
            SendState(message.Payload, message.Targets);
    }

    private static IEnumerable<uint> ConnectedRemoteIds()
    {
        foreach (var client in NetworkManager.Clients)
        {
            if (client == null || client.State != NetworkConnectionState.Connected) continue;
            if (client.ClientId == NetworkManager.ServerClientId) continue;

            yield return client.ClientId;
        }
    }

    private void SendState(byte[] payload, uint[] targets)
    {
        if (StateSender != null)
        {
            StateSender(payload, targets);
            return;
        }

        ReceiveState(payload, new NetworkRpcParams { TargetIds = targets });
    }

    private bool SendMetric(string name, float value)
    {
        if (ReportSender != null) return ReportSender(name, value);

        ReportMetric(name, value, new NetworkRpcParams());
        return true;
    }

    // RPCs are declared as in the engine documentation (public, context parameter with a default value).
    // They are called by this script only: the engine generates the code that sends them to the other peers.

    /// <summary>
    /// Server to client: the state a client may see. Runs on the clients that were targeted.
    /// </summary>
    [NetworkRpc(Client = true)]
    public void ReceiveState(byte[] payload, NetworkRpcParams rpc = new NetworkRpcParams())
    {
        ApplyState(payload, rpc.SenderId);
    }

    /// <summary>
    /// Client to server: a metric measured on the client. Runs on the server, with the sender's client id filled in by the engine.
    /// </summary>
    [NetworkRpc(Server = true)]
    public void ReportMetric(string name, float value, NetworkRpcParams rpc = new NetworkRpcParams())
    {
        HandleReport(rpc.SenderId, name, value);
    }

    internal bool ApplyState(byte[] payload, uint senderId) => OIDDANetworkPlugin.Cache?.Apply(payload, senderId) ?? false;

    internal bool HandleReport(uint senderId, string name, float value)
        => _session != null && _session.HandleReport(senderId, name, value, Time.UnscaledGameTime);

    private void OnClientConnected(NetworkClient client)
    {
        var manager = OIDDAPlugin.Instance?.Manager;
        if (manager && manager.DebugMode)
            Debug.Log($"[OIDDA Network] Client connected: ClientId={client.ClientId} (LocalClientId={NetworkManager.LocalClientId}, Mode={NetworkManager.Mode})");
    }

    private void OnClientDisconnected(NetworkClient client)
    {
        var manager = OIDDAPlugin.Instance?.Manager;
        if (manager && manager.DebugMode)
            Debug.Log($"[OIDDA Network] Client disconnected: ClientId={client.ClientId}");
    }
}
