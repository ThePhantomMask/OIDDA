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

    /// <summary> Id of the default group: every player starts in it, and it always exists while a group is configured. </summary>
    public const uint DefaultGroupId = 0;

    /// <summary>
    /// False on a dedicated server, where the default player is not a person: the group then leaves it out of its aggregation.
    /// Set by the network layer; true everywhere else.
    /// </summary>
    public bool DefaultPlayerIsReal { get; set; } = true;

    [EditorDisplay("Multiplayer"), Range(1, 256), Tooltip("Maximum number of players OIDDA keeps a context for. The default player counts only while it is a person: on a dedicated server (DefaultPlayerIsReal is false) it is left out, so 8 means 8 clients. Size it to your game.")]
    public int MaxPlayers = 8;

    /// <summary>
    /// The players that count towards <see cref="MaxPlayers"/>: every context, except the default one while it is not a person.
    /// </summary>
    private int CountedPlayers
    {
        get
        {
            _ = DefaultContext; // make sure the default context exists, so it is always in the count
            return DefaultPlayerIsReal ? _players.Count : _players.Count - 1;
        }
    }

    [EditorDisplay("Multiplayer"), Tooltip("Enables the group Director: it paces the whole session from the aggregated state of every player. Leave empty for no group.")]
    public JsonAssetReference<OIDDAGroupConfig> GroupConfig;

    [EditorDisplay("Multiplayer"), Tooltip("Thresholds and rates of the group Director. Only used when a group config is assigned.")]
    public DirectorManager GroupDirector = new();

    [EditorDisplay("Multiplayer"), Range(0, 64), Tooltip("Maximum number of groups besides the default group (group 0, which always exists and does not count). A group is created when a player is first assigned to it with SetPlayerGroup; groups that are empty and waiting to be removed do not count. 0 means only the default group.")]
    public int MaxGroups = 8;

    [EditorDisplay("Multiplayer"), Range(0, 3600), Tooltip("Seconds an empty group (not the default one) is kept before it is removed with its values and Director state, so that a player who comes back finds it as it was. 0 removes it as soon as it is empty.")]
    public float GroupGraceSeconds = 120f;

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

    // The default group (id 0), which uses GroupDirector. The other groups are created on demand and have a Director of their own.
    private OIDDAGroup _group;
    private readonly Dictionary<uint, OIDDAGroup> _otherGroups = new();
    private readonly List<OIDDAGroup> _extraGroups = new();
    private bool _warnedMaxGroups;

    /// <summary>
    /// The default group, id 0 (null unless a <see cref="GroupConfig"/> is assigned). Use <see cref="GetGroup"/> for the others.
    /// </summary>
    public OIDDAGroup Group => _group;

    /// <summary>
    /// The group with the given id, or null if it does not exist (or no group is configured).
    /// </summary>
    public OIDDAGroup GetGroup(uint groupId)
    {
        if (groupId == DefaultGroupId) return _group;
        return _otherGroups.TryGetValue(groupId, out var group) ? group : null;
    }

    /// <summary>
    /// Fills the list with the groups that exist: the default group first, then the others in the order they were created.
    /// The list is cleared first; it stays empty when no group is configured.
    /// </summary>
    public void GetGroups(List<OIDDAGroup> into)
    {
        into.Clear();
        if (_group == null) return;

        into.Add(_group);
        into.AddRange(_extraGroups);
    }

    /// <summary>
    /// Moves a player to a group, creating it if needed (up to <see cref="MaxGroups"/>). The player keeps their age, so there is no new
    /// warm-up. Without a group config only the id is stored: it takes effect when a group config is assigned.
    /// </summary>
    /// <returns>False if the player is not registered or the group cannot be created because <see cref="MaxGroups"/> is reached.</returns>
    public bool SetPlayerGroup(uint playerId, uint groupId)
    {
        var context = GetContext(playerId);
        if (context == null) return false;

        var previous = context.GroupId;
        if (previous == groupId) return true;

        if (_group != null)
        {
            var target = EnsureGroup(groupId);
            if (target == null) return false;

            context.GroupId = groupId;
            context.AttachGroup(target);
        }
        else
        {
            context.GroupId = groupId;
        }

        OnLeftGroup(previous);
        return true;
    }

    private void ConfigureGroup()
    {
        var config = GroupConfig ? GroupConfig.Instance : null;

        if (config == null)
        {
            _group = null;
            _otherGroups.Clear();
            _extraGroups.Clear();
            AttachGroups();
            return;
        }

        _group ??= new OIDDAGroup(this, GroupDirector);
        _group.Configure(config, _initialValues);
        foreach (var group in _extraGroups) group.Configure(config, _initialValues);
        AttachGroups();
    }

    private void AttachGroups()
    {
        AttachPlayerGroup(DefaultContext);
        for (int i = 0; i < _extraContexts.Count; i++) AttachPlayerGroup(_extraContexts[i]);
    }

    // Connects a context to the group its GroupId names, creating the group if it is missing. A player whose group cannot be created
    // (MaxGroups reached) goes to the default group.
    private void AttachPlayerGroup(OIDDAContext player)
    {
        if (_group == null)
        {
            player.AttachGroup(null);
            return;
        }

        var group = EnsureGroup(player.GroupId);
        if (group == null)
        {
            player.GroupId = DefaultGroupId;
            group = _group;
        }
        player.AttachGroup(group);
    }

    // The group with this id, created if it does not exist yet. Null when there is no group level or MaxGroups is reached.
    private OIDDAGroup EnsureGroup(uint groupId)
    {
        var existing = GetGroup(groupId);
        if (existing != null || _group == null) return existing;

        if (CountActiveExtraGroups() >= MaxGroups)
        {
            if (!_warnedMaxGroups)
            {
                _warnedMaxGroups = true;
                Debug.LogWarning($"[OIDDA] Cannot create group {groupId}: MaxGroups ({MaxGroups}) reached.");
            }
            return null;
        }

        var director = new DirectorManager();
        director.CopySettingsFrom(GroupDirector);

        var created = new OIDDAGroup(this, director, groupId);
        created.Configure(_group.Config, _initialValues);
        _otherGroups[groupId] = created;
        _extraGroups.Add(created);
        return created;
    }

    // Counts the players of every group besides the default one (into OIDDAGroup.Members).
    private void RecountMembers()
    {
        for (int i = 0; i < _extraGroups.Count; i++) _extraGroups[i].Members = 0;
        CountMember(DefaultContext);
        for (int i = 0; i < _extraContexts.Count; i++) CountMember(_extraContexts[i]);
    }

    private void CountMember(OIDDAContext player)
    {
        if (player.GroupId != DefaultGroupId && _otherGroups.TryGetValue(player.GroupId, out var group)) group.Members++;
    }

    // Groups that have players. Empty groups waiting to be removed are not counted.
    private int CountActiveExtraGroups()
    {
        if (_extraGroups.Count == 0) return 0;

        RecountMembers();
        var count = 0;
        for (int i = 0; i < _extraGroups.Count; i++)
            if (_extraGroups[i].Members > 0) count++;
        return count;
    }

    // A player left a group: with no grace time, a group that is now empty goes away at once.
    private void OnLeftGroup(uint groupId)
    {
        if (groupId == DefaultGroupId || GroupGraceSeconds > 0f) return;

        var group = GetGroup(groupId);
        if (group == null) return;

        RecountMembers();
        if (group.Members == 0) RemoveGroup(group);
    }

    private void RemoveGroup(OIDDAGroup group)
    {
        _otherGroups.Remove(group.Id);
        _extraGroups.Remove(group);
    }

    // Removes the groups that have been empty for longer than GroupGraceSeconds.
    private void UpdateGroupLifetimes(float deltaTime)
    {
        if (_extraGroups.Count == 0) return;

        RecountMembers();
        for (int i = _extraGroups.Count - 1; i >= 0; i--)
        {
            var group = _extraGroups[i];
            if (group.Members > 0)
            {
                group.EmptyFor = 0f;
                continue;
            }

            group.EmptyFor += deltaTime;
            if (group.EmptyFor >= GroupGraceSeconds) RemoveGroup(group);
        }
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
    /// The current state of the default group (the one that is replicated to clients), in the form clients receive. False when no group is configured.
    /// </summary>
    public bool TryGetGroupState(out OIDDAStateSnapshot snapshot) => TryGetGroupState(DefaultGroupId, out snapshot);

    /// <summary>
    /// The current state of a group, in the form clients receive. False if the group does not exist.
    /// </summary>
    public bool TryGetGroupState(uint groupId, out OIDDAStateSnapshot snapshot)
    {
        var group = GetGroup(groupId);
        if (group == null)
        {
            snapshot = default;
            return false;
        }

        snapshot = OIDDAStateSnapshot.From(group);
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
        if (existing) return existing;

        if (CountedPlayers >= MaxPlayers)
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
        if (config) context.ApplyConfig(config);

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
        OnLeftGroup(context.GroupId);
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
            OnLeftGroup(fresh.GroupId);
        }
        else if (CountedPlayers >= MaxPlayers)
        {
            Debug.LogWarning($"[OIDDA] Cannot resume player {playerId}: MaxPlayers ({MaxPlayers}) reached.");
            return null;
        }

        parked.PlayerId = playerId;
        AttachPlayerGroup(parked); // back in its own group, which is created again if it is gone

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
        OnLeftGroup(context.GroupId);
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

            if (_pluginInstance.CurrentConfig != null)
                context.ApplyConfig(_pluginInstance.CurrentConfig);
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
        if (_orsAgentDB.Count != 0) _orsAgentDB.Clear();
        if (_staticORSDB.Count != 0) _staticORSDB.Clear();
        foreach (var player in _extraContexts) player.Reset();
        _group?.Reset();
        foreach (var group in _extraGroups) group.Reset();
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
        for (int i = 0; i < _extraGroups.Count; i++) _extraGroups[i].Update(deltaTime, context, _extraContexts);
        UpdateGroupLifetimes(deltaTime);
    }

    #region Director Agent Management

    public void AddDirectorIntensity(float amount, string reason = "") => DefaultContext.AddDirectorIntensity(amount, reason);

    public bool IsShouldSpawnEncounter => DefaultContext.IsShouldSpawnEncounter;
    public bool IsInFlowState => DefaultContext.IsInFlow;
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
