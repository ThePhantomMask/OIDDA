using FlaxEditor.Content.Settings;
using FlaxEngine;
using FlaxEngine.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OIDDA;

/// <summary>
/// OIDDA Manager
/// </summary>
public class OIDDAManager : Script
{
    public int CurrentIndex;

    [Range(0, 1)]
    public float DifficultThreshold = 0.7f;

    [Range(0, 1)]
    public float EasyThreshold = 0.3f;

    [EditorDisplay("Smoothing"),Tooltip("Enable gradual value changes instead of instant")]
    public bool EnableSmoothing = true;

    [Tooltip("Enable debug logging")]
    public bool DebugMode = false;

    [Tooltip("Cooldown between adjustments (seconds)")]
    public float AdjustmentCooldown = 10f;

    Dictionary<string, IORSAgentD> ORSAgentDB = new();
    Dictionary<string, IORSAgentS> StaticORSDB = new();
    GameplayGlobals GameplayValues;
    float UpdateInterval, Delay, _timerSender, _timerReceiver, _timeSinceLastUpdate = 0f, _timeSinceLastAdjustment = 0f;

    OIDDAConfig _currentConfig;
    SmoothingManager _smoothingManager = new();
    bool InstantMetricsUpdated;

    public override void OnStart()
    {
        var OIDDA = Engine.GetCustomSettings("OIDDASettings").CreateInstance<OIDDASettings>();
        var Settings = GameSettings.Load();
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Settings.CompanyName, Settings.ProductName, "OIDDA");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        OIDDAInit(OIDDA);
    }
    
    public override void OnEnable()
    {
        // Here you can add code that needs to be called when script is enabled (eg. register for events)
    }

    public override void OnDisable()
    {
        OIDDAReset();
    }

    internal void OIDDAInit(OIDDASettings settings)
    {
        if (settings is null) return;
        GameplayValues = settings.Globals[CurrentIndex];
        settings.StaticORS.ForEach(kv => StaticORSDB.Add(kv.Key, kv.Value));
        if (settings.Configs.Count != 0) _currentConfig = settings.Configs[CurrentIndex].Instance;
        UpdateInterval = settings.UpdateInterval;
        Delay = settings.Delay;
    }

    void OIDDAReset()
    {
        if (GameplayValues) GameplayValues.ResetValues();  
        if (ORSAgentDB.Capacity != 0) ORSAgentDB.Clear(); 
        if (StaticORSDB.Capacity != 0) StaticORSDB.Clear();
    }

    void AnalyzeAndApply()
    {
        if (_currentConfig == null || _currentConfig.Rules == null || _currentConfig.Metrics == null ||
            _currentConfig.Rules.Count == 0 ||  _currentConfig.Metrics.Count == 0) return;

        if (_timeSinceLastAdjustment < AdjustmentCooldown) return;

        float debugOverallScore = 0f;

        if (DebugMode)
        {
           var analyze = MetricsAggregator.Analyze(_currentConfig.Metrics, GameplayValues.Values);
           debugOverallScore = analyze.OverallScore;
           LogAnalysis(analyze);
        }

        var overallScore = (DebugMode) ? debugOverallScore : MetricsAggregator.CalculateOverallScore(_currentConfig.Metrics, GameplayValues.Values);

        if (_timeSinceLastAdjustment < dynamicCooldown(overallScore)) return; 

        int rulesApplied = ApplyRules(GameplayValues.Values, overallScore);
        
        if (rulesApplied > 0)
        {
            _timeSinceLastAdjustment = 0f;

            if (DebugMode)
            {
                Debug.Log($"OIDDA applied {rulesApplied} rules.");

                if (EnableSmoothing && _smoothingManager.HasActiveSmoothings)
                {
                    Debug.Log($"[OIDDA] Smoothing {_smoothingManager.ActiveSmoothingCount} value(s)");
                }
            }
        }
    }

    float dynamicCooldown(float score) => score < EasyThreshold ? AdjustmentCooldown * 0.5f : score > DifficultThreshold ? AdjustmentCooldown * 1.0f : AdjustmentCooldown;

    int ApplyRules(Dictionary<string, object> currentValues, float overallScore)
    {
        int rulesApplied = 0;
        foreach (var rule in _currentConfig.Rules)
        {
            if (rule.Condition != null && !rule.Condition.IsMet(currentValues)) continue;
            if (!ShouldApplyRule(overallScore, rule)) continue;

            if (EnableSmoothing)
            {
                ApplyRuleSmooth(rule, currentValues);
                rulesApplied++;
                return rulesApplied;
            }

            rule.Apply(currentValues);
            rulesApplied++;
        }
        return rulesApplied;
    }

    void ApplyRuleSmooth(OIDDARule rule, Dictionary<string, object> currentValues)
    {
        try
        {
            var targetValue = GameplayValue.FromObject(currentValues[rule.TargetGlobalVariable]);
            var newValue = GameplayValueOperations.Apply(targetValue, rule.AdjustmentValue, rule.Operator);
            newValue = GameplayValueOperations.Clamp(newValue, rule.MinValue, rule.MaxValue);
            _smoothingManager.SetTarget(rule.TargetGlobalVariable, newValue, _currentConfig.SmoothingSpeed);

            if (DebugMode)
            {
                Debug.Log($"[OIDDA] Smoothing: {rule.TargetGlobalVariable} " +
                          $"{targetValue.GetValue()} -> {newValue.GetValue()} " +
                          $"(speed: {_currentConfig.SmoothingSpeed})");
            }

        }
        catch (Exception e)
        {
            Debug.LogError($"[OIDDA] Error in smooth apply: {e.Message}");
        }
    }

    bool ShouldApplyRule(float overallScore, OIDDARule rule)
    {
        if (rule is OIDDARuleException ruleException)
        {
            return ruleException.ApplicationContext switch
            {
                RuleApplicationContext.Always => true,
                RuleApplicationContext.WhenTooDifficult => overallScore > DifficultThreshold,
                RuleApplicationContext.WhenTooEasy => overallScore < EasyThreshold,
                RuleApplicationContext.WhenBalanced => overallScore >= EasyThreshold && overallScore <= DifficultThreshold,
                _ => false,
            };
        }

        return (overallScore > DifficultThreshold) ? rule.Operator is AdjustmentOperator.Subtract || rule.Operator is AdjustmentOperator.Set :
            (overallScore < EasyThreshold) ? rule.Operator is AdjustmentOperator.Add || rule.Operator is AdjustmentOperator.Multiply : false;
    }

    void LogAnalysis(MetricsAnalysis analysis)
    {
        Debug.Log($"=== OIDDA Analysis ===");
        Debug.Log($"Overall Score: {analysis.OverallScore:F3} ({analysis.OverallState})");
        Debug.Log($"Individual Metrics:");

        analysis.MetricInfos.ForEach(info => Debug.Log($"[{info.State}] {info.MetricName}: {info.NormalizedScore: F3}" +
            $"(weighted: {info.WeightedScore: F3}, value: {info.CurrentValue})"));

        var problematic = MetricsAggregator.GetProblematicMetrics(_currentConfig.Metrics, GameplayValues.Values, DifficultThreshold);
        if (problematic.Count > 0)
        {
            Debug.Log($"Problematic Metrics ({problematic.Count}):");
            problematic.ForEach(metric => Debug.LogWarning($"{metric.MetricName}: {metric.NormalizedScore:F3}"));
        }
    }

    void OIDDAUpdate()
    {
        if (EnableSmoothing)
        {
            _smoothingManager.SmoothUpdate(Time.DeltaTime);
        }

        _timeSinceLastUpdate += Time.DeltaTime;
        _timeSinceLastAdjustment += Time.DeltaTime;

        if (InstantMetricsUpdated)
        {
            InstantMetricsUpdated = false; _timeSinceLastUpdate = 0f;
            return;
        }

        if (_timeSinceLastUpdate >= UpdateInterval)
        {
            AnalyzeAndApply();
            _timeSinceLastUpdate -= UpdateInterval;
        }
    }

    #region ORS Agent Management

    public bool Connect(string AgentName)
    {
        if (StaticORSDB.ContainsKey(AgentName))
        {
            var agent = StaticORSDB[AgentName];
            agent.TotalORSAgentsConnected++;
            StaticORSDB[AgentName] = agent;
            return true; 
        }
        return false;
    }

    public bool Connect(string ID, IORSAgentD agentD)
    {
        if (!ORSAgentDB.ContainsKey(ID))
        {
            ORSAgentDB.Add(ID, agentD);
            return true;
        }
        return false;
    }

    public bool Disconnect(string AgentName)
    {
        if (StaticORSDB.ContainsKey(AgentName))
        {
            var agent = StaticORSDB[AgentName];
            agent.TotalORSAgentsConnected--;
            StaticORSDB[AgentName] = agent;
            return true;
        }
        return false;
    }

    public bool Disconnect(string ID, ORSUtils.ORSType type)
    {
        if (ORSAgentDB.ContainsKey(ID))
        {
            ORSAgentDB.Remove(ID);
            return true;
        }
        return false;
    }

    public bool ORSIsConnected(string ID) => ORSAgentDB.ContainsKey(ID);

    public bool StaticORSIsConnected(string name) => StaticORSDB.ContainsKey(name) && StaticORSDB[name].ORSStatus is ORSUtils.ORSStatus.Connected;

    void DelaySender(string name, object value)
    {
        _timerSender += Time.DeltaTime;
        if (_timerSender >= Delay)
        {
            AnalyzeAndApply();
            GameplayValues.SetValue(name, value);
            _timerSender = 0;
        }
    }

    T DelayReceiver<T>(string name)
    {
        _timerReceiver += Time.DeltaTime;
        if (_timerReceiver >= Delay)
        {
            _timerReceiver = 0;
            return GameplayValues.GetValue(name) is T typeValue ? typeValue : default(T);
        }
        return default(T);
    }

    public bool VerifyIsReceiver(string ID) => ORSAgentDB[ID].ORSType == ORSUtils.ORSType.ReceiverSender || ORSAgentDB[ID].ORSType == ORSUtils.ORSType.Receiver;

    public bool VerifyIsStaticReceiver(string Name) => StaticORSDB[Name].ORSType == ORSUtils.ORSType.ReceiverSender || StaticORSDB[Name].ORSType == ORSUtils.ORSType.Receiver;

    public bool VerifyIsSender(string ID) => ORSAgentDB[ID].ORSType == ORSUtils.ORSType.ReceiverSender || ORSAgentDB[ID].ORSType == ORSUtils.ORSType.Sender;

    public bool VerifyIsStaticSender(string Name) => StaticORSDB[Name].ORSType == ORSUtils.ORSType.ReceiverSender || StaticORSDB[Name].ORSType == ORSUtils.ORSType.Sender;

    public void SetGlobal(string name, object value) => (Delay != 0f ? (Action)(() => DelaySender(name, value)) : () => GameplayValues.SetValue(name, value))();

    public void SetStaticGlobal(string NameAgent, object value) => (Delay != 0f ? (Action)(() => DelaySender(StaticORSDB[NameAgent].GlobalVariable, value)) : () => { AnalyzeAndApply(); GameplayValues.SetValue(StaticORSDB[NameAgent].GlobalVariable, value); })();

    public void QuickSender(string name, object value) { GameplayValues.SetValue(name, value); AnalyzeAndApply(); }

    public T GetGlobal<T>(string name) => (Delay != 0f) ? DelayReceiver<T>(name) : GameplayValues.GetValue(name) is T typeValue ? typeValue : default(T);

    public T GetStaticGlobal<T>(string NameAgent) => (Delay != 0f) ? DelayReceiver<T>(StaticORSDB[NameAgent].GlobalVariable) : GameplayValues.GetValue(StaticORSDB[NameAgent].GlobalVariable) is T typeValue ? typeValue : default(T);

    public T QuickReceiver<T>(string name) => GameplayValues.GetValue(name) is T typeValue ? typeValue : default(T);
    #endregion

    public override void OnUpdate()
    {
        OIDDAUpdate();
    }
}
