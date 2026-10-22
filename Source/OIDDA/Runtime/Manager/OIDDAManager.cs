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

    private OIDDAPlugin _pluginInstance;

    private Dictionary<string, IORSAgentD> _orsAgentDB = new();
    private Dictionary<string, IORSAgentS> _staticORSDB = new();

    // Loaded from the plugin settings; read by the contexts.
    internal float UpdateInterval, Delay;

    private OIDDAContext _default;

    /// <summary>
    /// The context used by single-player and by the default (local) player.
    /// </summary>
    public OIDDAContext DefaultContext => _default ??= new OIDDAContext(this, Director);

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

            if (_pluginInstance.CurrentOIDDAConfig)
                context.ApplyConfig(_pluginInstance.CurrentOIDDAConfig.Instance);
        }
        context.ResetTimers();
    }

    void OIDDAReset()
    {
        DefaultContext.Reset();
        if (_orsAgentDB.Count != 0) _orsAgentDB.Clear();
        if (_staticORSDB.Count != 0) _staticORSDB.Clear();
    }

    void OIDDAUpdate()
    {
        var context = DefaultContext;

        // The plugin fills CurrentGlobals on scene load, possibly after this script's OnEnable: retry until available.
        if (!context.HasStore)
        {
            Initialize();
            if (!context.HasStore) return;
        }

        context.Update(Time.DeltaTime);
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

    public T GetStaticGlobal<T>(string NameAgent) => DefaultContext.GetGlobal<T>(_staticORSDB[NameAgent].GlobalVariable);

    public T QuickReceiver<T>(string name) => DefaultContext.QuickReceiver<T>(name);
    #endregion

    public override void OnUpdate()
    {
        OIDDAUpdate();
    }
}
