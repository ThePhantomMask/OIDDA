using FlaxEditor.Content.Settings;
using FlaxEngine;
using FlaxEngine.Utilities;
using OIDDA.Data;
using System;
using System.Collections.Generic;
using System.IO;
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
    public float DifficultThreshold = 0.7f;

    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("OIDDA Manager"), Range(0, 1)]
    public float EasyThreshold = 0.3f;

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
    private float _updateInterval, _delay, _timerSender, _timerReceiver, _score, _timeSinceLastUpdate = 0f, _timeSinceLastAdjustment = 0f;

    private GameplayGlobals _currentGlobal;
    private SmoothingManager _smoothingManager = new();
    private MetricsAnalysis _analyze;

    public override void OnStart()
    {
        _pluginInstance = OIDDAPlugin.Instance;
        
        if (_pluginInstance)
        {
            var OIDDASettings = _pluginInstance.Settings;

            if (OIDDASettings != null)
            {
                Director.isDirectorSmoothing = _isUseSmoothing = OIDDASettings.UseDDASmoothing;
                _updateInterval = OIDDASettings.UpdateInterval;
                _delay = OIDDASettings.Delay;
                _isUseDirector = OIDDASettings.UseDirector;
            }
        }
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
        if (_pluginInstance)
        {
            if (_pluginInstance.CurrentStaticORSAgents != null && _pluginInstance.CurrentStaticORSAgents.Count > 0)
                _pluginInstance.CurrentStaticORSAgents.ForEach(kv => _staticORSDB[kv.Key] = kv.Value);
            _currentGlobal = _pluginInstance.CurrentGlobals;
            if (_pluginInstance.CurrentOIDDAConfig) Director.currentConfig = _pluginInstance.CurrentOIDDAConfig.Instance;
        }
    }

    void OIDDAReset()
    {
        if (_currentGlobal) _currentGlobal.ResetValues();  
        if (_orsAgentDB.Count != 0) _orsAgentDB.Clear(); 
        if (_staticORSDB.Count != 0) _staticORSDB.Clear();
    }

    void AnalyzeAndApply()
    {
        if (Director.currentConfig == null || Director.currentConfig.Rules == null || Director.currentConfig.Metrics == null ||
            Director.currentConfig.Rules.Count == 0 || Director.currentConfig.Metrics.Count == 0) return;

        if (_timeSinceLastAdjustment < AdjustmentCooldown) return;

        if (DebugMode) LogAnalysis(_analyze = MetricsAggregator.Analyze(Director.currentConfig.Metrics, _pluginInstance.CurrentGlobals.Values));

        _score = (DebugMode) ? _analyze.OverallScore : MetricsAggregator.CalculateOverallScore(Director.currentConfig.Metrics, _pluginInstance.CurrentGlobals.Values);

        if (_isUseDirector) _score = ApplyDirectorInfluence(_score);
        if (_timeSinceLastAdjustment < dynamicCooldown(_score)) return; 

        int rulesApplied = ApplyRules(_pluginInstance.CurrentGlobals.Values, _score);
        
        if (rulesApplied > 0)
        {
            _timeSinceLastAdjustment = 0f;

            if (DebugMode)
            {
                if (_isUseSmoothing && _smoothingManager.HasActiveSmoothings)
                {
                    Debug.Log($"[OIDDA] Smoothing {_smoothingManager.ActiveSmoothingCount} value(s)");
                }
            }
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

    float dynamicCooldown(float score)
    {
        var baseCooldown = score < EasyThreshold ? AdjustmentCooldown * 0.5f : score > DifficultThreshold ? AdjustmentCooldown * 1.0f : AdjustmentCooldown;

        // Change cooldown based on director status
        if (_isUseDirector)
        {
            baseCooldown *= Director.CurrentState switch
            {
                DirectorState.Peak => 0.7f,    // Faster during peaks
                DirectorState.Relax => 2.0f,   // Slower during rest
                _ => 1.0f
            };
        }

        return baseCooldown;
    }

    int ApplyRules(Dictionary<string, object> currentValues, float overallScore)
    {
        int rulesApplied = 0;
        foreach (var rule in Director.currentConfig.Rules)
        {
            if (rule.Condition != null && !rule.Condition.IsMet(currentValues)) continue;
            if (!ShouldApplyRule(overallScore, rule)) continue;

            if(_isUseSmoothing) ApplyRuleSmooth(rule, currentValues);
            rule.Apply(currentValues);
            rulesApplied++;
        }
        return rulesApplied;
    }

    void ApplyRuleSmooth(Rule rule, Dictionary<string, object> currentValues)
    {
        try
        {
            var targetValue = GameplayValue.ConvertObject(currentValues[rule.TargetGlobal]);
            var newValue = GameplayValueOperations.Apply(targetValue, rule.Value, rule.Operator);
            newValue = GameplayValueOperations.Clamp(newValue, rule.MinValue, rule.MaxValue);
            _smoothingManager.SetTarget(rule.TargetGlobal, newValue, Director.currentConfig.SmoothingSpeed);

            if (DebugMode)
            {
                Debug.Log($"[OIDDA] Smoothing: {rule.TargetGlobal} " +
                          $"{targetValue.Value} -> {newValue.Value} " +
                          $"(speed: {Director.currentConfig.SmoothingSpeed})");
            }

        }
        catch (Exception e)
        {
            Debug.LogError($"[OIDDA] Error in smooth apply: {e.Message}");
        }
    }

    bool ShouldApplyRule(float overallScore, Rule rule)
    {
        return (rule is RuleException ruleException) ? ruleException.Context switch
        {
            RuleApplicationContext.Always => true,
            RuleApplicationContext.WhenTooDifficult => overallScore > DifficultThreshold,
            RuleApplicationContext.WhenTooEasy => overallScore < EasyThreshold,
            RuleApplicationContext.WhenBalanced => overallScore >= EasyThreshold && overallScore <= DifficultThreshold,
            _ => false,
        } :
        (overallScore > DifficultThreshold) ? rule.Operator == AdjustmentOperator.Subtract || rule.Operator == AdjustmentOperator.Set :
            (overallScore < EasyThreshold) ? rule.Operator == AdjustmentOperator.Add || rule.Operator == AdjustmentOperator.Multiply : false;
    }

    void LogAnalysis(MetricsAnalysis analysis)
    {
        var analysisLog = new StringBuilder();
        analysisLog.AppendLine("OIDDA Analysis:");
        analysisLog.AppendLine($"Overall Score: {analysis.OverallScore:F3} ({analysis.OverallState})");
        analysisLog.AppendLine($"Individual Metrics: {string.Join("\n", analysis.MetricInfos.Select(info => $"[{info.State}] {info.MetricName}: {info.NormalizedScore: F3}"))}");

        var problematic = MetricsAggregator.GetProblematicMetrics(Director.currentConfig.Metrics, _pluginInstance.CurrentGlobals.Values, DifficultThreshold);
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
        if (!_currentGlobal)
            return;

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

    void DelaySender(string name, object value)
    {
        _timerSender += Time.DeltaTime;
        if (_timerSender >= _delay)
        {
            AnalyzeAndApply();
            _currentGlobal.SetValue(name, value);
            _timerSender = 0;
        }
    }

    T DelayReceiver<T>(string name)
    {
        _timerReceiver += Time.DeltaTime;
        if (_timerReceiver >= _delay)
        {
            _timerReceiver = 0;
            return _currentGlobal.GetValue<T>(name);
        }
        return default(T);
    }

    public bool VerifyIsReceiver(string ID) => _orsAgentDB[ID].ORSType == ORSType.ReceiverSender || _orsAgentDB[ID].ORSType == ORSType.Receiver;

    public bool VerifyIsStaticReceiver(string Name) => _staticORSDB[Name].ORSType == ORSType.ReceiverSender || _staticORSDB[Name].ORSType == ORSType.Receiver;

    public bool VerifyIsSender(string ID) => _orsAgentDB[ID].ORSType == ORSType.ReceiverSender || _orsAgentDB[ID].ORSType == ORSType.Sender;

    public bool VerifyIsStaticSender(string Name) => _staticORSDB[Name].ORSType == ORSType.ReceiverSender || _staticORSDB[Name].ORSType == ORSType.Sender;

    public void SetGlobal(string name, object value) => (_delay != 0f ? (Action)(() => DelaySender(name, value)) : () => _currentGlobal.SetValue(name, value))();

    public void SetStaticGlobal(string NameAgent, object value) => (_delay != 0f ? (Action)(() => DelaySender(_staticORSDB[NameAgent].GlobalVariable, value)) : () => { AnalyzeAndApply(); _currentGlobal.SetValue(_staticORSDB[NameAgent].GlobalVariable, value); })();

    public void QuickSender(string name, object value) { _currentGlobal.SetValue(name, value); AnalyzeAndApply(); }

    public T GetGlobal<T>(string name) => (_delay != 0f) ? DelayReceiver<T>(name) : _currentGlobal.GetValue<T>(name);

    public T GetStaticGlobal<T>(string NameAgent) => (_delay != 0f) ? DelayReceiver<T>(_staticORSDB[NameAgent].GlobalVariable) : _currentGlobal.GetValue<T>(_staticORSDB[NameAgent].GlobalVariable);

    public T QuickReceiver<T>(string name) => _currentGlobal.GetValue<T>(name);
    #endregion

    public override void OnUpdate()
    {
        OIDDAUpdate();
    }
}