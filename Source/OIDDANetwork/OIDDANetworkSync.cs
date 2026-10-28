using FlaxEngine;
using FlaxEngine.Networking;
using System;
using System.Collections.Generic;

namespace OIDDA.Network;

/// <summary>
/// Place one in the scene (on the same actor as the OIDDAManager works), on every peer. 
/// On the server or host it keeps OIDDA's players in step with the connected clients and sends each client the state it may see; 
/// on a client it receives that state and, if enabled, sends metrics measured on the client.
/// </summary>
public class OIDDANetworkSync : Script
{
    [EditorDisplay("State"), Tooltip("Off: each client only receives its own player's state (and the group's). On: every client receives every player's state, for example to show teammates in the UI.")]
    public bool ReplicateStateToAll = false;

    [EditorDisplay("State"), Range(0.02f, 2f), Tooltip("Seconds between state messages to the clients.")]
    public float StateSendInterval = 0.1f;

    [EditorDisplay("Reconnection"), Range(0f, 3600f), Tooltip("Seconds the server keeps a disconnected player's state so they get it back when they reconnect (they are recognised by a secret key their client sends). 0 turns reconnection off.")]
    public float ReconnectGraceSeconds = 120f;

    [EditorDisplay("Reconnection"), Tooltip("Client side: send the player key to the server so a reconnection is recognised. Turn off to never identify this client.")]
    public bool SendPlayerKey = true;

    [EditorDisplay("Client metrics"), Tooltip("Lets clients report metrics that only exist on the client (for example a reaction time). Off by default: the server is the only source of metrics.")]
    public bool AllowClientMetrics = false;

    [EditorDisplay("Client metrics"), Range(1, 100), Tooltip("Reports per second accepted from each client.")]
    public int MaxReportsPerSecond = 10;

    [EditorDisplay("Client metrics"), Collection(Display = CollectionAttribute.DisplayType.Header), Tooltip("The only metrics clients may report. Values are clamped to the range given here.")]
    public List<AllowedClientMetric> AllowedClientMetrics = new();

    // Test seams: where state and reports go when they are not sent through the network.
    internal Action<byte[], uint[]> StateSender;
    internal Func<string, float, bool> ReportSender;
    internal Action<string> IdentifySender;

    private OIDDAServerSession _session;
    private ManagerPlayerHost _host;
    private OIDDAMetricGate _gate;
    private Func<string, float, bool> _reporter;
    private readonly List<uint> _remoteIds = new();
    private float _timer;
    private bool _logged;
    private bool _identified;
    private bool _duplicate;
    private int _lastRejected;
    private OIDDASessionStats _lastStats;

    // Only one script may run: two would send every client the state twice and register players twice.
    private static OIDDANetworkSync _active;

    internal static void ForgetActive() => _active = null;

    /// <inheritdoc />
    public override void OnEnable()
    {
        if (_active && _active != this)
        {
            _duplicate = true;
            Debug.LogWarning("[OIDDA Network] There is already an active OIDDANetworkSync. Keep only one in the scene; this one does nothing.");
            return;
        }

        _active = this;
        _duplicate = false;

        // The editor limits these, but values loaded from a scene are not checked.
        if (StateSendInterval < 0.02f) StateSendInterval = 0.02f;
        if (ReconnectGraceSeconds < 0f) ReconnectGraceSeconds = 0f;
        if (MaxReportsPerSecond < 1) MaxReportsPerSecond = 1;

        NetworkReplicator.AddObject(this);

        _gate = AllowClientMetrics ? new OIDDAMetricGate(AllowedClientMetrics, MaxReportsPerSecond) : null;

        NetworkManager.ClientConnected += OnClientConnected;
        NetworkManager.ClientDisconnected += OnClientDisconnected;
        NetworkManager.StateChanged += OnNetworkStateChanged;
        _identified = false;

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
        if (_duplicate)
        {
            _duplicate = false;
            return;
        }

        if (_active == this) _active = null;

        NetworkManager.ClientConnected -= OnClientConnected;
        NetworkManager.ClientDisconnected -= OnClientDisconnected;
        NetworkManager.StateChanged -= OnNetworkStateChanged;

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

        if (mode == NetworkManagerMode.Client) ClientTick();
        else if (mode == NetworkManagerMode.Server || mode == NetworkManagerMode.Host) ServerTick(Time.UnscaledDeltaTime);
    }

    // Once per connection, tell the server who we are so that a reconnection can be recognised.
    internal void ClientTick()
    {
        if (_duplicate) return;
        if (_identified || !SendPlayerKey || NetworkManager.State != NetworkConnectionState.Connected) return;

        _identified = true;
        SendIdentify(OIDDAPlayerKey.Current());
    }

    private void OnNetworkStateChanged()
    {
        if (NetworkManager.State != NetworkConnectionState.Connected) _identified = false;
    }

    private void SendIdentify(string key)
    {
        if (IdentifySender != null)
        {
            IdentifySender(key);
            return;
        }

        Identify(key);
    }

    internal void ServerTick(float deltaTime)
    {
        if (_duplicate) return;

        var manager = OIDDAPlugin.Instance?.Manager;
        if (!manager) return;

        // A new scene brings a new manager: start over with it.
        if (_host == null || _host.Manager != manager)
        {
            _host = new ManagerPlayerHost(manager);
            _session = new OIDDAServerSession(_host, _gate, ReconnectGraceSeconds);
            _timer = 0f;
            _lastRejected = 0;
            _lastStats = default;
        }

        _timer += deltaTime;
        if (_timer < StateSendInterval) return;
        _timer = 0f;

        bool defaultPlayerIsReal = NetworkManager.Mode != NetworkManagerMode.Server;

        FillConnectedRemoteIds(_remoteIds);
        _session.Reconcile(_remoteIds, defaultPlayerIsReal, Time.UnscaledGameTime);

        ReportProblems(manager);

        if (manager.DebugMode && !_logged)
        {
            _logged = true;
            Debug.Log($"[OIDDA Network] Mode={NetworkManager.Mode}, LocalClientId={NetworkManager.LocalClientId}, remote clients=[{string.Join(", ", _session.Players)}]");
        }

        foreach (var message in _session.BuildMessages(ReplicateStateToAll, defaultPlayerIsReal))
            SendState(message.Payload, message.Targets);
    }

    // Clients the manager refused are a configuration problem worth a warning (once per change, not every tick).
    private void ReportProblems(OIDDAManager manager)
    {
        int rejected = _session.Rejected.Count;
        if (rejected != _lastRejected)
        {
            if (rejected > _lastRejected)
                Debug.LogWarning($"[OIDDA Network] {rejected} client(s) have no player: OIDDAManager.MaxPlayers ({manager.MaxPlayers}) is reached. Raise it to fit your game.");
            _lastRejected = rejected;
        }

        if (!manager.DebugMode) return;

        var stats = _session.Stats;
        if (stats.Parked != _lastStats.Parked || stats.Resumed != _lastStats.Resumed || stats.Expired != _lastStats.Expired || stats.Evicted != _lastStats.Evicted)
        {
            Debug.Log($"[OIDDA Network] Reconnection: kept={_session.ParkedCount}, parked={stats.Parked}, resumed={stats.Resumed}, expired={stats.Expired}, evicted={stats.Evicted}");
            _lastStats = stats;
        }
    }

    private static void FillConnectedRemoteIds(List<uint> ids)
    {
        ids.Clear();

        var clients = NetworkManager.Clients;
        for (int i = 0; i < clients.Length; i++)
        {
            var client = clients[i];
            if (client == null || client.State != NetworkConnectionState.Connected) continue;
            if (client.ClientId == NetworkManager.ServerClientId) continue;

            ids.Add(client.ClientId);
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

    /// <summary>
    /// Client to server: the player key, once per connection. Runs on the server, with the sender's client id filled in by the engine.
    /// </summary>
    [NetworkRpc(Server = true)]
    public void Identify(string key, NetworkRpcParams rpc = new NetworkRpcParams())
    {
        HandleIdentify(rpc.SenderId, key);
    }

    internal IdentifyResult HandleIdentify(uint senderId, string key)
    {
        var result = _session?.HandleIdentify(senderId, key, Time.UnscaledGameTime) ?? IdentifyResult.Rejected;

        var manager = OIDDAPlugin.Instance?.Manager;
        if (manager && manager.DebugMode && result != IdentifyResult.Rejected)
            Debug.Log($"[OIDDA Network] Client {senderId} identified: {result}");

        return result;
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
