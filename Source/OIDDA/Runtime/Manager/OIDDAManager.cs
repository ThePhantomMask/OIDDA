using FlaxEditor.Content.Settings;
using FlaxEngine;
using FlaxEngine.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace OIDDA;

/// <summary>
/// OIDDA Manager
/// </summary>
public class OIDDAManager : Script
{
    Dictionary<string, IORSAgentD> ORSAgentDB = new();
    Dictionary<string, IORSAgentS> StaticORSDB = new();
    Dictionary<string, object> _currentMetrics = new();
    GameplayGlobals GameplayValues;
    float UpdateInterval, Delay, _timerSender, _timerReceiver;

    public string CurrentSceneTag;

    [Range(0, 2)]
    public float DifficultThreshold = 1.7f;

    [Range(0, 2)]
    public float EasyThreshold = 0.3f;

    [Tooltip("Enable debug logging")]
    public bool DebugMode = false;

    OIDDAConfig _currentConfig;

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
        GameplayValues = !string.IsNullOrEmpty(CurrentSceneTag) ? settings.Globals[CurrentSceneTag] : settings.Globals.Values.FirstOrDefault();
        StaticORSDB = settings.StaticORS.DeepClone();
        _currentMetrics = GameplayValues.Values.DeepClone();
        _currentConfig = settings.Configs.Values.FirstOrDefault().Instance.DeepClone();
        UpdateInterval = settings.UpdateInterval;
        Delay = settings.Delay;
    }

    void OIDDAReset()
    {
        if (GameplayValues) GameplayValues.ResetValues(); 
        if(_currentMetrics.Capacity != 0) _currentMetrics.Clear(); 
        if (ORSAgentDB.Capacity != 0) ORSAgentDB.Clear(); 
        if (StaticORSDB.Capacity != 0) StaticORSDB.Clear();
    }

    void AnalyzeAndApply()
    {
        if (_currentConfig == null || _currentConfig.Rules.Count is 0 || _currentConfig.Metrics.Count is 0) return;

        var analysis = MetricsAggregator.Analyze(_currentConfig.Metrics, _currentMetrics);

        if (DebugMode) LogAnalysis(analysis);

        foreach (var rule in _currentConfig.Rules)
        {
            if (ShouldApplyRule(analysis.OverallScore, rule))
                rule.Apply(_currentMetrics);
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

    void MetricsToGlobals() => _currentMetrics.ForEach(metric => GameplayValues.SetValue(metric.Key, metric.Value));

    void LogAnalysis(MetricsAnalysis analysis)
    {
        Debug.Log($"=== OIDDA Analysis ===");
        Debug.Log($"Overall Score: {analysis.OverallScore:F3} ({analysis.OverallState})");
        Debug.Log($"Individual Metrics:");

        analysis.MetricInfos.ForEach(info => Debug.Log($"[{info.State}] {info.MetricName}: {info.NormalizedScore: F3}" +
            $"(weighted: {info.WeightedScore: F3}, value: {info.CurrentValue})"));

        var problematic = MetricsAggregator.GetProblematicMetrics(_currentConfig.Metrics, _currentMetrics, DifficultThreshold);
        if (problematic.Count > 0)
        {
            Debug.Log($"Problematic Metrics ({problematic.Count}):");
            problematic.ForEach(metric => Debug.LogWarning($"{metric.MetricName}: {metric.NormalizedScore:F3}"));
        }
    }

    void OIDDAUpdate()
    {
        if (InstantMetricsUpdated)
        {
            MetricsToGlobals();
            InstantMetricsUpdated = false;
            return;
        }

        if (Time.TimeSinceStartup % UpdateInterval < Time.DeltaTime)
        {
            AnalyzeAndApply(); MetricsToGlobals();
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

    public bool ORSIsConnected() => StaticORSDB.Values.Any(agent => agent.ORSStatus is ORSUtils.ORSStatus.Connected);

    void DelaySender(string name, object value)
    {
        _timerSender += Time.DeltaTime;
        if (_timerSender >= Delay)
        {
            _currentMetrics[name] = value;
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

    public void SetGlobal(string name, object value) => (Delay != 0f ? (Action)(() => DelaySender(name, value)) : () => _currentMetrics[name] = value)();

    public void SetStaticGlobal(string NameAgent, object value) => (Delay != 0f ? (Action)(() => DelaySender(StaticORSDB[NameAgent].GlobalVariable, value)) : () => _currentMetrics[StaticORSDB[NameAgent].GlobalVariable] = value)();

    public void QuickSender(string name, object value) => _currentMetrics[name] = value;

    public T GetGlobal<T>(string name) => (Delay != 0f) ? DelayReceiver<T>(name) : GameplayValues.GetValue(name) is T typeValue ? typeValue : default(T);

    public T GetStaticGlobal<T>(string NameAgent) => (Delay != 0f) ? DelayReceiver<T>(StaticORSDB[NameAgent].GlobalVariable) : GameplayValues.GetValue(StaticORSDB[NameAgent].GlobalVariable) is T typeValue ? typeValue : default(T);

    public T QuickReceiver<T>(string name) => GameplayValues.GetValue(name) is T typeValue ? typeValue : default(T);
    #endregion

    public override void OnUpdate()
    {
        OIDDAUpdate();
    }
}
