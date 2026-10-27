using FlaxEngine;
using OIDDA.Data;
using System.Collections.Generic;

namespace OIDDA;

/// <summary>
/// OIDDA Manager
/// </summary>
[Category(name: "OIDDA")]
public class OIDDAManager : Script
{
    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("OIDDA Manager"), Range(0, 1)]
    public float DifficultThreshold = OIDDADefaults.DifficultThreshold;

    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("OIDDA Manager"), Range(0, 1)]
    public float EasyThreshold = OIDDADefaults.EasyThreshold;

    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("OIDDA Manager"), Tooltip("Enable debug logging")]
    public bool DebugMode = false;

    [EditorDisplay("Smoothing"), Tooltip("Cooldown between adjustments (seconds)")]
    public float AdjustmentCooldown = 10f;

    [Range(0, 1), EditorDisplay("Director"), Tooltip("Influence of pacing on difficulty adjustments (0-1)")]
    public float DirectorInfluence = 0.7f;

    public DirectorManager Director = new();

    /// <summary> Id of the default (local) player. Single-player always uses it. </summary>
    public const uint DefaultPlayerId = 0;

    /// <summary>
    /// False on a dedicated server, where the default player is not a person: the group then leaves it out of its aggregation.
    /// Set by the network layer; true everywhere else.
    /// </summary>
    public bool DefaultPlayerIsReal { get; set; } = true;

    [EditorDisplay("Multiplayer"), Range(1, 256), Tooltip("Maximum number of players OIDDA keeps a context for, including the default one. Size it to your game.")]
    public int MaxPlayers = 8;

    [EditorDisplay("Multiplayer"), Tooltip("Enables the group Director: it paces the whole session from the aggregated state of every player. Leave empty for no group.")]
    public JsonAssetReference<OIDDAGroupConfig> GroupConfig;

    [EditorDisplay("Multiplayer"), Tooltip("Thresholds and rates of the group Director. Only used when a group config is assigned.")]
    public DirectorManager GroupDirector = new();

    private OIDDAPlugin _pluginInstance;

    private Dictionary<string, IORSAgentD> _orsAgentDB = new();
    private Dictionary<string, IORSAgentS> _staticORSDB = new();

    // Loaded from the plugin settings; read by the contexts.
    internal float UpdateInterval, Delay;

    private OIDDAContext _default;

    // Every player's context by id (the default one included), and the same contexts without the default one in
    // registration order, so the update loop does not enumerate a dictionary.
    private readonly Dictionary<uint, OIDDAContext> _players = new();
    private readonly List<OIDDAContext> _extraContexts = new();

    // Variables as they were when the manager was initialized: what a newly registered player starts from.
    private Dictionary<string, object> _initialValues;

    private OIDDAGroup _group;

    /// <summary>
    /// The group level (null unless a <see cref="GroupConfig"/> is assigned).
    /// </summary>
    public OIDDAGroup Group => _group;

    private void ConfigureGroup()
    {
        var config = GroupConfig ? GroupConfig.Instance : null;

        if (config == null)
        {
            _group = null;
            AttachGroup(null);
            return;
        }

        _group ??= new OIDDAGroup(this, GroupDirector);
        _group.Configure(config, _initialValues);
        AttachGroup(_group);
    }

    private void AttachGroup(OIDDAGroup group)
    {
        DefaultContext.AttachGroup(group);
        foreach (var player in _extraContexts) player.AttachGroup(group);
    }

    /// <summary>
    /// The context used by single-player and by the default (local) player. Its values live in the scene's GameplayGlobals.
    /// </summary>
    public OIDDAContext DefaultContext
    {
        get
        {
            if (_default == null)
            {
                _default = new OIDDAContext(this, Director) { PlayerId = DefaultPlayerId };
                _players[DefaultPlayerId] = _default;
            }
            return _default;
        }
    }

    #region State for clients

    /// <summary>
    /// The current state of a player, in the form clients receive. False if the player is not registered.
    /// </summary>
    public bool TryGetPlayerState(uint playerId, out OIDDAStateSnapshot snapshot)
    {
        var context = GetContext(playerId);
        if (context == null)
        {
            snapshot = default;
            return false;
        }

        snapshot = OIDDAStateSnapshot.From(context);
        return true;
    }

    /// <summary>
    /// The current state of the group, in the form clients receive. False when no group is configured.
    /// </summary>
    public bool TryGetGroupState(out OIDDAStateSnapshot snapshot)
    {
        if (_group == null)
        {
            snapshot = default;
            return false;
        }

        snapshot = OIDDAStateSnapshot.From(_group);
        return true;
    }

    #endregion

    #region Players

    /// <summary> Number of players with a context, the default one included. </summary>
    public int PlayerCount => System.Math.Max(1, _players.Count);

    /// <summary> Every player's context by id, the default one included. </summary>
    public IReadOnlyDictionary<uint, OIDDAContext> Players
    {
        get
        {
            _ = DefaultContext;
            return _players;
        }
    }

    public bool HasPlayer(uint playerId) => playerId == DefaultPlayerId || _players.ContainsKey(playerId);

    /// <summary>
    /// The context of a player, or null if that player is not registered.
    /// </summary>
    public OIDDAContext GetContext(uint playerId)
    {
        if (playerId == DefaultPlayerId) return DefaultContext;
        return _players.TryGetValue(playerId, out var context) ? context : null;
    }

    /// <summary>
    /// Creates the context of a player. Each player has their own values, personal Director and smoothing; the configuration
    /// and the manager settings are shared. Registering an existing player returns their context.
    /// </summary>
    /// <param name="playerId">Any id that is unique for the player (a local index, or a network client id).</param>
    /// <param name="initialValues">Starting values. When null, the variables as they were when the manager was initialized.</param>
    /// <returns>The context, or null if <see cref="MaxPlayers"/> has been reached.</returns>
    public OIDDAContext RegisterPlayer(uint playerId, IEnumerable<KeyValuePair<string, object>> initialValues = null)
    {
        var existing = GetContext(playerId);
        if (existing != null) return existing;

        if (_players.Count >= MaxPlayers)
        {
            Debug.LogWarning($"[OIDDA] Cannot register player {playerId}: MaxPlayers ({MaxPlayers}) reached.");
            return null;
        }

        var director = new DirectorManager();
        director.CopySettingsFrom(Director);

        var context = new OIDDAContext(this, director) { PlayerId = playerId };
        context.SetStore(new DictionaryValueStore(initialValues ?? _initialValues));
        context.AttachGroup(_group);

        var config = DefaultContext.Director.currentConfig;
        if (config != null) context.ApplyConfig(config);

        // Spread the periodic analysis of the players over different frames (golden-ratio steps).
        context.StaggerTimers(((_extraContexts.Count + 1) * 0.6180339887f) % 1f);

        _players[playerId] = context;
        _extraContexts.Add(context);
        return context;
    }

    /// <summary>
    /// Takes a player out of play without losing their state: the context stops updating, no longer counts towards
    /// <see cref="MaxPlayers"/> and cannot be found by id. Hand it back with <see cref="ResumePlayer"/> (typically under a new id,
    /// when the player reconnects). The state is frozen while parked.
    /// </summary>
    /// <returns>The parked context, or null if the player is unknown or is the default player.</returns>
    public OIDDAContext ParkPlayer(uint playerId)
    {
        if (playerId == DefaultPlayerId || !_players.TryGetValue(playerId, out var context)) return null;

        _players.Remove(playerId);
        _extraContexts.Remove(context);
        return context;
    }

    /// <summary>
    /// Puts a context taken out with <see cref="ParkPlayer"/> back in play under the given id. A fresh context that was
    /// already created under that id is replaced. The player's values, Director state and age are kept.
    /// </summary>
    /// <returns>The context, or null if it cannot be resumed (default player id, already in play, or <see cref="MaxPlayers"/> reached).</returns>
    public OIDDAContext ResumePlayer(uint playerId, OIDDAContext parked)
    {
        if (parked == null || playerId == DefaultPlayerId || _extraContexts.Contains(parked)) return null;

        if (_players.TryGetValue(playerId, out var fresh))
        {
            _players.Remove(playerId);
            _extraContexts.Remove(fresh);
        }
        else if (_players.Count >= MaxPlayers)
        {
            Debug.LogWarning($"[OIDDA] Cannot resume player {playerId}: MaxPlayers ({MaxPlayers}) reached.");
            return null;
        }

        parked.PlayerId = playerId;
        parked.AttachGroup(_group);

        _players[playerId] = parked;
        _extraContexts.Add(parked);
        return parked;
    }

    /// <summary>
    /// Removes a player's context. The default player cannot be removed.
    /// </summary>
    public bool UnregisterPlayer(uint playerId)
    {
        if (playerId == DefaultPlayerId || !_players.TryGetValue(playerId, out var context)) return false;

        _players.Remove(playerId);
        _extraContexts.Remove(context);
        return true;
    }

    #endregion

    public override void OnStart()
    {
        LoadSettings();
    }

    void LoadSettings()
    {
        // OnEnable runs before OnStart: never rely on OnStart having cached the plugin.
        if (!_pluginInstance) _pluginInstance = OIDDAPlugin.Instance;
        if (!_pluginInstance) return;

        var OIDDASettings = _pluginInstance.Settings;
        if (OIDDASettings == null) return;

        UpdateInterval = OIDDASettings.UpdateInterval;
        Delay = OIDDASettings.Delay;
    }

    public override void OnEnable()
    {
        Initialize();
    }

    public override void OnDisable()
    {
        OIDDAReset();
    }

    private void Initialize()
    {
        LoadSettings();

        var context = DefaultContext;
        context.BindDirector(Director);

        if (_pluginInstance)
        {
            if (_pluginInstance.CurrentStaticORSAgents != null && _pluginInstance.CurrentStaticORSAgents.Count > 0)
                _pluginInstance.CurrentStaticORSAgents.ForEach(kv => _staticORSDB[kv.Key] = kv.Value);

            var globals = _pluginInstance.CurrentGlobals;
            context.SetStore(globals ? new GameplayGlobalsStore(globals) : null);
            _initialValues = globals ? new Dictionary<string, object>(globals.Values) : null;

            if (_pluginInstance.CurrentOIDDAConfig)
                context.ApplyConfig(_pluginInstance.CurrentOIDDAConfig.Instance);
        }
        context.ResetTimers();

        // Players registered before this point get the variables and configuration that were not available yet.
        var sharedConfig = context.Director.currentConfig;
        foreach (var player in _extraContexts)
        {
            (player.Store as DictionaryValueStore)?.SeedMissing(_initialValues);
            if (sharedConfig != null) player.ApplyConfig(sharedConfig);
        }

        ConfigureGroup();
    }

    void OIDDAReset()
    {
        DefaultContext.Reset();
        foreach (var player in _extraContexts) player.Reset();
        _group?.Reset();
        if (_orsAgentDB.Count != 0) _orsAgentDB.Clear();
        if (_staticORSDB.Count != 0) _staticORSDB.Clear();
    }

    void OIDDAUpdate()
    {
        // A remote client does not run OIDDA: the server does, and its state reaches the ORS through the state provider.
        if (ORS.IsRemoteClient) return;

        var context = DefaultContext;

        // The plugin fills CurrentGlobals on scene load, possibly after this script's OnEnable: retry until available.
        if (!context.HasStore)
        {
            Initialize();
            if (!context.HasStore) return;
        }

        var deltaTime = Time.DeltaTime;
        context.Update(deltaTime);

        // Index loop: a context can be registered or removed from a callback while the others update.
        for (int i = 0; i < _extraContexts.Count; i++)
        {
            var player = _extraContexts[i];
            if (player.HasStore) player.Update(deltaTime);
        }

        _group?.Update(deltaTime, context, _extraContexts);
    }

    #region Director Agent Management

    public void AddDirectorIntensity(float amount, string reason = "") => DefaultContext.AddDirectorIntensity(amount, reason);

    public bool IsShouldSpawnEncounter => DefaultContext.IsShouldSpawnEncounter;
    public DirectorState DirectorState => DefaultContext.DirectorState;
    public float Intensity => DefaultContext.Intensity;
    public float PlayerStress => DefaultContext.PlayerStress;
    public float PlayerFatigue => DefaultContext.PlayerFatigue;

    #endregion

    #region ORS Agent Management

    public bool Connect(string AgentName)
    {
        if (_staticORSDB.ContainsKey(AgentName))
        {
            var agent = _staticORSDB[AgentName];
            agent.TotalORSAgentsConnected++;
            _staticORSDB[AgentName] = agent;
            return true; 
        }
        return false;
    }

    public bool Connect(string ID, IORSAgentD agentD)
    {
        if (!_orsAgentDB.ContainsKey(ID))
        {
            _orsAgentDB.Add(ID, agentD);
            return true;
        }
        return false;
    }

    public bool Disconnect(string AgentName)
    {
        if (_staticORSDB.ContainsKey(AgentName))
        {
            var agent = _staticORSDB[AgentName];
            agent.TotalORSAgentsConnected--;
            _staticORSDB[AgentName] = agent;
            return true;
        }
        return false;
    }

    public bool Disconnect(string ID, ORSType type)
    {
        if (_orsAgentDB.ContainsKey(ID))
        {
            _orsAgentDB.Remove(ID);
            return true;
        }
        return false;
    }

    public bool ORSIsConnected(string ID) => _orsAgentDB.ContainsKey(ID);

    public bool StaticORSIsConnected(string name) => _staticORSDB.ContainsKey(name) && _staticORSDB[name].ORSStatus is ORSStatus.Connected;

    public bool VerifyIsReceiver(string ID) => _orsAgentDB[ID].ORSType.CanReceive();

    public bool VerifyIsStaticReceiver(string Name) => _staticORSDB[Name].ORSType.CanReceive();

    public bool VerifyIsSender(string ID) => _orsAgentDB[ID].ORSType.CanSend();

    public bool VerifyIsStaticSender(string Name) => _staticORSDB[Name].ORSType.CanSend();

    public void SetGlobal(string name, object value) => DefaultContext.SetGlobal(name, value);

    public void SetStaticGlobal(string NameAgent, object value) => DefaultContext.SetStaticGlobal(_staticORSDB[NameAgent].GlobalVariable, value);

    public void QuickSender(string name, object value) => DefaultContext.QuickSender(name, value);

    /// <summary>
    /// Writes a value without triggering an analysis (internal use by rules and smoothing).
    /// </summary>
    internal void WriteGlobal(string name, object value) => DefaultContext.WriteGlobal(name, value);

    public T GetGlobal<T>(string name) => DefaultContext.GetGlobal<T>(name);

    /// <summary> Name of the gameplay variable behind a static ORS agent. </summary>
    internal string StaticVariable(string agentName) => _staticORSDB[agentName].GlobalVariable;

    public T GetStaticGlobal<T>(string NameAgent) => DefaultContext.GetGlobal<T>(_staticORSDB[NameAgent].GlobalVariable);

    public T QuickReceiver<T>(string name) => DefaultContext.QuickReceiver<T>(name);
    #endregion

    public override void OnUpdate()
    {
        OIDDAUpdate();
    }
}
