using FlaxEditor.Content.Settings;
using FlaxEngine;
using FlaxEngine.Utilities;
using OIDDA.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OIDDA;

/// <summary>
/// OIDDA Manager
/// </summary>
[Category(name: "OIDDA")]
public class OIDDAManager : Script
{
    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("OIDDA Manager")]
    public int CurrentIndex;
    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("OIDDA Manager")]
    public bool InstantMetricsUpdated;

    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("OIDDA Manager"), Range(0, 1)]
    public float DifficultThreshold = 0.7f;

    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("OIDDA Manager"), Range(0, 1)]
    public float EasyThreshold = 0.3f;

    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("OIDDA Manager"), Tooltip("Enable debug logging")]
    public bool DebugMode = false;

    [EditorDisplay("Smoothing"), Tooltip("Cooldown between adjustments (seconds)")]
    public float AdjustmentCooldown = 10f;


    bool isUseSmoothing;
    Dictionary<string, IORSAgentD> ORSAgentDB = new();
    Dictionary<string, IORSAgentS> StaticORSDB = new();
    GameplayGlobals GameplayValues;
    float updateInterval, delay, timerSender, timerReceiver, score, timeSinceLastUpdate = 0f, timeSinceLastAdjustment = 0f;

    OIDDASettings OIDDASettings;
    OIDDAConfig currentConfig;
    SmoothingManager smoothingManager = new();
    MetricsAnalysis analyze;

    public override void OnStart()
    {
        OIDDASettings = Engine.GetCustomSettings("OIDDASettings").CreateInstance<OIDDASettings>();
        var Settings = GameSettings.Load();
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Settings.CompanyName, Settings.ProductName, OIDDASettings.FolderName);
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        OIDDAInit(OIDDASettings);
    }

    public override void OnDisable()
    {
        OIDDAReset();
    }

    internal void OIDDAInit(OIDDASettings settings)
    {
        if (settings is null) return;
        GameplayValues = settings.Globals[CurrentIndex];
        settings.StaticORSGroup[CurrentIndex].ForEach(kv => StaticORSDB.Add(kv.Key, kv.Value));
        if (settings.Configs.Count != 0) currentConfig = settings.Configs[CurrentIndex].Instance;
        isUseSmoothing = settings.UseDDASmoothing;
        updateInterval = settings.UpdateInterval;
        delay = settings.Delay;
    }

    void OIDDAReset()
    {
        if (GameplayValues) GameplayValues.ResetValues();  
        if (ORSAgentDB.Count != 0) ORSAgentDB.Clear(); 
        if (StaticORSDB.Count != 0) StaticORSDB.Clear();
    }

    void AnalyzeAndApply()
    {
        if (currentConfig == null || currentConfig.Rules == null || currentConfig.Metrics == null ||
            currentConfig.Rules.Count == 0 ||  currentConfig.Metrics.Count == 0) return;

        if (timeSinceLastAdjustment < AdjustmentCooldown) return;

        if (DebugMode) LogAnalysis(analyze = MetricsAggregator.Analyze(currentConfig.Metrics, GameplayValues.Values));

        score = (DebugMode) ? analyze.OverallScore : MetricsAggregator.CalculateOverallScore(currentConfig.Metrics, GameplayValues.Values);

        if (timeSinceLastAdjustment < dynamicCooldown(score)) return; 

        int rulesApplied = ApplyRules(GameplayValues.Values, score);
        
        if (rulesApplied > 0)
        {
            timeSinceLastAdjustment = 0f;

            if (DebugMode)
            {
                Debug.Log($"OIDDA applied {rulesApplied} rules.");

                if (isUseSmoothing && smoothingManager.HasActiveSmoothings)
                {
                    Debug.Log($"[OIDDA] Smoothing {smoothingManager.ActiveSmoothingCount} value(s)");
                }
            }
        }
    }

    float dynamicCooldown(float score) => score < EasyThreshold ? AdjustmentCooldown * 0.5f : score > DifficultThreshold ? AdjustmentCooldown * 1.0f : AdjustmentCooldown;

    int ApplyRules(Dictionary<string, object> currentValues, float overallScore)
    {
        int rulesApplied = 0;
        foreach (var rule in currentConfig.Rules)
        {
            if (rule.Condition != null && !rule.Condition.IsMet(currentValues)) continue;
            if (!ShouldApplyRule(overallScore, rule)) continue;

            if(isUseSmoothing) ApplyRuleSmooth(rule, currentValues);
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
            smoothingManager.SetTarget(rule.TargetGlobal, newValue, currentConfig.SmoothingSpeed);

            if (DebugMode)
            {
                Debug.Log($"[OIDDA] Smoothing: {rule.TargetGlobal} " +
                          $"{targetValue.Value} -> {newValue.Value} " +
                          $"(speed: {currentConfig.SmoothingSpeed})");
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
        Debug.Log($"=== OIDDA Analysis ===");
        Debug.Log($"Overall Score: {analysis.OverallScore:F3} ({analysis.OverallState})");
        Debug.Log($"Individual Metrics:");

        analysis.MetricInfos.ForEach(info => Debug.Log($"[{info.State}] {info.MetricName}: {info.NormalizedScore: F3}" +
            $"(weighted: {info.WeightedScore: F3}, value: {info.CurrentValue})"));

        var problematic = MetricsAggregator.GetProblematicMetrics(currentConfig.Metrics, GameplayValues.Values, DifficultThreshold);
        if (problematic.Count > 0)
        {
            Debug.Log($"Problematic Metrics ({problematic.Count}):");
            problematic.ForEach(metric => Debug.LogWarning($"{metric.MetricName}: {metric.NormalizedScore:F3}"));
        }
    }

    void OIDDAUpdate()
    {
        if (isUseSmoothing) smoothingManager.SmoothUpdate(Time.DeltaTime);
        timeSinceLastUpdate += Time.DeltaTime;
        timeSinceLastAdjustment += Time.DeltaTime;

        if (InstantMetricsUpdated)
        {
            InstantMetricsUpdated = false; timeSinceLastUpdate = 0f;
            return;
        }

        if (timeSinceLastUpdate >= updateInterval)
        {
            AnalyzeAndApply();
            timeSinceLastUpdate -= updateInterval;
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

    public bool Disconnect(string ID, ORSType type)
    {
        if (ORSAgentDB.ContainsKey(ID))
        {
            ORSAgentDB.Remove(ID);
            return true;
        }
        return false;
    }

    public bool ORSIsConnected(string ID) => ORSAgentDB.ContainsKey(ID);

    public bool StaticORSIsConnected(string name) => StaticORSDB.ContainsKey(name) && StaticORSDB[name].ORSStatus is ORSStatus.Connected;

    void DelaySender(string name, object value)
    {
        timerSender += Time.DeltaTime;
        if (timerSender >= delay)
        {
            AnalyzeAndApply();
            GameplayValues.SetValue(name, value);
            timerSender = 0;
        }
    }

    T DelayReceiver<T>(string name)
    {
        timerReceiver += Time.DeltaTime;
        if (timerReceiver >= delay)
        {
            timerReceiver = 0;
            return GameplayValues.GetValue(name) is T typeValue ? typeValue : default(T);
        }
        return default(T);
    }

    public bool VerifyIsReceiver(string ID) => ORSAgentDB[ID].ORSType == ORSType.ReceiverSender || ORSAgentDB[ID].ORSType == ORSType.Receiver;

    public bool VerifyIsStaticReceiver(string Name) => StaticORSDB[Name].ORSType == ORSType.ReceiverSender || StaticORSDB[Name].ORSType == ORSType.Receiver;

    public bool VerifyIsSender(string ID) => ORSAgentDB[ID].ORSType == ORSType.ReceiverSender || ORSAgentDB[ID].ORSType == ORSType.Sender;

    public bool VerifyIsStaticSender(string Name) => StaticORSDB[Name].ORSType == ORSType.ReceiverSender || StaticORSDB[Name].ORSType == ORSType.Sender;

    public void SetGlobal(string name, object value) => (delay != 0f ? (Action)(() => DelaySender(name, value)) : () => GameplayValues.SetValue(name, value))();

    public void SetStaticGlobal(string NameAgent, object value) => (delay != 0f ? (Action)(() => DelaySender(StaticORSDB[NameAgent].GlobalVariable, value)) : () => { AnalyzeAndApply(); GameplayValues.SetValue(StaticORSDB[NameAgent].GlobalVariable, value); })();

    public void QuickSender(string name, object value) { GameplayValues.SetValue(name, value); AnalyzeAndApply(); }

    public T GetGlobal<T>(string name) => (delay != 0f) ? DelayReceiver<T>(name) : GameplayValues.GetValue(name) is T typeValue ? typeValue : default(T);

    public T GetStaticGlobal<T>(string NameAgent) => (delay != 0f) ? DelayReceiver<T>(StaticORSDB[NameAgent].GlobalVariable) : GameplayValues.GetValue(StaticORSDB[NameAgent].GlobalVariable) is T typeValue ? typeValue : default(T);

    public T QuickReceiver<T>(string name) => GameplayValues.GetValue(name) is T typeValue ? typeValue : default(T);
    #endregion

    public override void OnUpdate()
    {
        OIDDAUpdate();
    }
}
