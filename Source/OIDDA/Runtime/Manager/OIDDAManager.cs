using FlaxEngine;
using OIDDA.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

    private bool _isUseSmoothing, _isUseDirector;
    private Dictionary<string, IORSAgentD> _orsAgentDB = new();
    private Dictionary<string, IORSAgentS> _staticORSDB = new();
    private float _updateInterval, _delay, _timeSinceLastUpdate = 0f, _timeSinceLastAdjustment = 0f;

    // Delay (per variable): last send/receive time, the newest value waiting to be sent, and the last value read.
    private readonly Dictionary<string, float> _lastSendTime = new(), _lastReceiveTime = new();
    private readonly Dictionary<string, object> _pendingSends = new(), _receiveCache = new();
    private readonly List<string> _flushKeys = new();

    private GameplayGlobals _currentGlobal;
    private SmoothingManager _smoothingManager = new();

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

        _updateInterval = OIDDASettings.UpdateInterval;
        _delay = OIDDASettings.Delay;
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

        if (_pluginInstance)
        {
            if (_pluginInstance.CurrentStaticORSAgents != null && _pluginInstance.CurrentStaticORSAgents.Count > 0)
                _pluginInstance.CurrentStaticORSAgents.ForEach(kv => _staticORSDB[kv.Key] = kv.Value);
            _currentGlobal = _pluginInstance.CurrentGlobals;
            if (_pluginInstance.CurrentOIDDAConfig)
            {
                Director.currentConfig = _pluginInstance.CurrentOIDDAConfig.Instance;
                _isUseSmoothing = Director.currentConfig.UseDDASmoothing;
                Director.isDirectorSmoothing = Director.currentConfig.OverrideDirectorSmoothing
                    ? Director.currentConfig.UseDirectorSmoothing
                    : _isUseSmoothing;
                _isUseDirector = Director.currentConfig.UseDirector;
            }
        }
        _timeSinceLastUpdate = _timeSinceLastAdjustment = 0;
    }

    void OIDDAReset()
    {
        if (_currentGlobal) _currentGlobal.ResetValues();  
        if (_orsAgentDB.Count != 0) _orsAgentDB.Clear(); 
        if (_staticORSDB.Count != 0) _staticORSDB.Clear();
        _smoothingManager.Clear();
        Director.ResetSmoothing();
        _lastSendTime.Clear();
        _lastReceiveTime.Clear();
        _pendingSends.Clear();
        _receiveCache.Clear();
    }

    bool _isAnalyzing;

    void AnalyzeAndApply()
    {
        // Safety net against re-entrancy (e.g. user code calling QuickSender from a rule callback).
        if (_isAnalyzing) return;

        _isAnalyzing = true;
        try { AnalyzeAndApplyInternal(); }
        finally { _isAnalyzing = false; }
    }

    void AnalyzeAndApplyInternal()
    {
        var config = Director.currentConfig;
        if (config == null || config.Rules == null || config.Metrics == null ||
            config.Rules.Count == 0 || config.Metrics.Count == 0) return;

        // Cheap early exit: nothing below can use a cooldown shorter than the smallest multiplier.
        if (_timeSinceLastAdjustment < AdjustmentCooldown * MinCooldownFactor) return;

        // GameplayGlobals.Values can be a marshalled copy: read it once per cycle.
        var values = _pluginInstance.CurrentGlobals.Values;
        float score;

        if (DebugMode)
        {
            var analysis = MetricsAggregator.Analyze(config.Metrics, values, EasyThreshold, DifficultThreshold);
            LogAnalysis(analysis);
            score = analysis.OverallScore;
        }
        else
        {
            score = MetricsAggregator.CalculateOverallScore(config.Metrics, values);
        }

        if (_isUseDirector) score = ApplyDirectorInfluence(score);
        if (_timeSinceLastAdjustment < dynamicCooldown(score)) return;

        int rulesApplied = ApplyRules(values, score);

        if (rulesApplied > 0)
        {
            _timeSinceLastAdjustment = 0f;

            if (DebugMode && _isUseSmoothing && _smoothingManager.HasActiveSmoothings)
                Debug.Log($"[OIDDA] Smoothing {_smoothingManager.ActiveSmoothingCount} value(s)");
        }
    }

    /// <summary>
    /// Calculates an adjusted score by applying the current pacing influence and difficulty multiplier to the specified base score.
    /// </summary>
    /// <remarks>The adjustment uses a linear interpolation between the base score and the base score
    /// multiplied by the current difficulty multiplier, weighted by the pacing influence. The returned value reflects
    /// dynamic game pacing and may change as pacing parameters are updated.</remarks>
    /// <param name="baseScore">The original score to be modified based on pacing and difficulty. Must be a finite, non-negative value.</param>
    /// <returns>A floating-point value representing the base score adjusted for pacing and difficulty. The result may be higher or lower than the input depending on the current pacing state.</returns>
    float ApplyDirectorInfluence(float baseScore)
    {
        var pacingMultiplier = Director.DifficultyMultiplier;
        var adjustedScore = Mathf.Lerp(baseScore, baseScore * pacingMultiplier, DirectorInfluence);

        if (DebugMode)
        {
            Debug.Log($"[Director] Base Score: {baseScore:F2} -> Adjusted: {adjustedScore:F2} " +
                     $"(Multiplier: {pacingMultiplier:F2}, State: {Director.CurrentState})");
        }

        return adjustedScore;
    }

    const float EasyCooldownFactor = 0.5f, PeakCooldownFactor = 0.7f, RelaxCooldownFactor = 2.0f;

    // Smallest multiplier dynamicCooldown can return (easy score, and Peak when the Director is on).
    float MinCooldownFactor => _isUseDirector ? EasyCooldownFactor * PeakCooldownFactor : EasyCooldownFactor;

    float dynamicCooldown(float score)
    {
        var baseCooldown = score < EasyThreshold ? AdjustmentCooldown * EasyCooldownFactor : AdjustmentCooldown;

        // Change cooldown based on director status
        if (_isUseDirector)
        {
            baseCooldown *= Director.CurrentState switch
            {
                DirectorState.Peak => PeakCooldownFactor,    // Faster during peaks
                DirectorState.Relax => RelaxCooldownFactor,  // Slower during rest
                _ => 1.0f
            };
        }

        return baseCooldown;
    }

    int ApplyRules(Dictionary<string, object> currentValues, float overallScore)
    {
        var config = Director.currentConfig;
        var ctx = new DifficultyContext(overallScore, EasyThreshold, DifficultThreshold);
        int rulesApplied = 0;

        foreach (var rule in config.Rules)
        {
            if (rule == null) continue;
            if (RuleApplier.ApplyRule(rule, currentValues, ctx, _isUseSmoothing, _smoothingManager, config.SmoothingSpeed, DebugMode))
                rulesApplied++;
        }
        return rulesApplied;
    }

    void LogAnalysis(MetricsAnalysis analysis)
    {
        var analysisLog = new StringBuilder();
        analysisLog.AppendLine("OIDDA Analysis:");
        analysisLog.AppendLine($"Overall Score: {analysis.OverallScore:F3} ({analysis.OverallState})");
        analysisLog.AppendLine($"Individual Metrics: {string.Join("\n", analysis.MetricInfos.Select(info => $"[{info.State}] {info.MetricName}: {info.NormalizedScore:F3}"))}");

        var problematic = analysis.MetricInfos.Where(info => info.NormalizedScore > DifficultThreshold)
                                              .OrderByDescending(info => info.NormalizedScore)
                                              .ToList();
        if (problematic.Count > 0)
        {
            analysisLog.AppendLine($"Problematic Metrics ({problematic.Count}):");
            problematic.ForEach(metric => analysisLog.AppendLine($"{metric.MetricName}: {metric.NormalizedScore:F3}"));
        }

        if (_isUseDirector) analysisLog.AppendLine($"[Director] {Director.DebugInfo}");

        Debug.Log(analysisLog.ToString());
    }

    void OIDDAUpdate()
    {
        // The plugin fills CurrentGlobals on scene load, possibly after this script's OnEnable: retry until available.
        if (!_currentGlobal)
        {
            Initialize();
            if (!_currentGlobal) return;
        }

        Director.DebugMode = DebugMode;

        if (_delay != 0f) FlushPendingSends();
        if (_isUseSmoothing) _smoothingManager.SmoothUpdate(Time.DeltaTime);
        if (_isUseDirector) Director.OnDirectorUpdate(Time.DeltaTime, _currentGlobal);
        _timeSinceLastUpdate += Time.DeltaTime;
        _timeSinceLastAdjustment += Time.DeltaTime;

        if (_timeSinceLastUpdate >= _updateInterval)
        {
            AnalyzeAndApply();
            _timeSinceLastUpdate -= _updateInterval;
        }
    }

    #region Director Agent Management

    public void AddDirectorIntensity(float amount, string reason = "")
    {
        if (!_isUseDirector) return;
        Director.AddIntensity(amount, reason);

        if (DebugMode) Debug.Log($"[Director] Intensity added: + {amount} ({reason})");
    }

    public bool IsShouldSpawnEncounter => _isUseDirector ? Director.ShouldSpawnEncounter() : true;
    public DirectorState DirectorState => Director.CurrentState;
    public float Intensity => Director.CurrentIntensity;
    public float PlayerStress => Director.StressLevel;
    public float PlayerFatigue => Director.FatigueLevel;

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

    // Each variable has its own clock (game time, so it does not depend on how many calls are made per frame).
    // The newest value is kept and written once the delay has passed, instead of being dropped.
    void DelaySender(string name, object value)
    {
        if (!_lastSendTime.ContainsKey(name)) _lastSendTime[name] = Time.GameTime;
        _pendingSends[name] = value;
    }

    void FlushPendingSends()
    {
        if (_pendingSends.Count == 0) return;

        var now = Time.GameTime;
        _flushKeys.Clear();
        foreach (var pending in _pendingSends)
            if (now - _lastSendTime[pending.Key] >= _delay) _flushKeys.Add(pending.Key);

        foreach (var key in _flushKeys)
        {
            var value = _pendingSends[key];
            _pendingSends.Remove(key);
            _lastSendTime[key] = now;

            AnalyzeAndApply();
            _currentGlobal.SetValue(key, value);
        }
    }

    // Reads the real value at most once per delay; in between, the last valid value is returned (never default).
    T DelayReceiver<T>(string name)
    {
        var now = Time.GameTime;

        if (_lastReceiveTime.TryGetValue(name, out var last) && now - last < _delay &&
            _receiveCache.TryGetValue(name, out var cached) && cached is T cachedValue)
            return cachedValue;

        var value = _currentGlobal.GetValue<T>(name);
        _receiveCache[name] = value;
        _lastReceiveTime[name] = now;
        return value;
    }

    public bool VerifyIsReceiver(string ID) => _orsAgentDB[ID].ORSType.CanReceive();

    public bool VerifyIsStaticReceiver(string Name) => _staticORSDB[Name].ORSType.CanReceive();

    public bool VerifyIsSender(string ID) => _orsAgentDB[ID].ORSType.CanSend();

    public bool VerifyIsStaticSender(string Name) => _staticORSDB[Name].ORSType.CanSend();

    public void SetGlobal(string name, object value)
    {
        if (_delay != 0f) DelaySender(name, value);
        else _currentGlobal.SetValue(name, value);
    }

    public void SetStaticGlobal(string NameAgent, object value)
    {
        var variable = _staticORSDB[NameAgent].GlobalVariable;

        if (_delay != 0f)
        {
            DelaySender(variable, value);
            return;
        }

        AnalyzeAndApply();
        _currentGlobal.SetValue(variable, value);
    }

    public void QuickSender(string name, object value) { _currentGlobal.SetValue(name, value); AnalyzeAndApply(); }

    /// <summary>
    /// Writes a value without triggering an analysis (internal use by rules and smoothing).
    /// </summary>
    internal void WriteGlobal(string name, object value) => _currentGlobal.SetValue(name, value);

    public T GetGlobal<T>(string name) => (_delay != 0f) ? DelayReceiver<T>(name) : _currentGlobal.GetValue<T>(name);

    public T GetStaticGlobal<T>(string NameAgent) => GetGlobal<T>(_staticORSDB[NameAgent].GlobalVariable);

    public T QuickReceiver<T>(string name) => _currentGlobal.GetValue<T>(name);
    #endregion

    public override void OnUpdate()
    {
        OIDDAUpdate();
    }
}