using FlaxEditor.Content.Settings;
using FlaxEngine;
using FlaxEngine.Utilities;
using System;
using System.Collections.Generic;
using System.IO;

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

    [EditorDisplay("Smoothing"), Tooltip("Enable gradual value changes instead of instant")]
    public bool EnableSmoothing = true;

    [EditorDisplay("Smoothing"), Tooltip("Cooldown between adjustments (seconds)")]
    public float AdjustmentCooldown = 10f;

    [EditorDisplay("Pacing Director"), Tooltip("Enable psychological pacing system")]
    public bool EnablePacing = true;

    [Range(0, 1), Tooltip("Influence of pacing on difficulty adjustments (0-1)")]
    public float PacingInfluence = 0.7f;

    public PacingDirector Director = new();

    Dictionary<string, IORSAgentD> ORSAgentDB = new();
    Dictionary<string, IORSAgentS> StaticORSDB = new();
    GameplayGlobals GameplayValues;
    float _updateInterval, _delay, _timerSender, _timerReceiver, _score, _timeSinceLastUpdate = 0f, _timeSinceLastAdjustment = 0f;

    OIDDAConfig _currentConfig;
    SmoothingManager _smoothingManager = new();
    MetricsAnalysis _analyze;

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
        _updateInterval = settings.UpdateInterval;
        _delay = settings.Delay;
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

        if (DebugMode) LogAnalysis(_analyze = MetricsAggregator.Analyze(_currentConfig.Metrics, GameplayValues.Values));

        _score = (DebugMode) ? _analyze.OverallScore : MetricsAggregator.CalculateOverallScore(_currentConfig.Metrics, GameplayValues.Values);

        if (EnablePacing) _score = ApplyPacingInfluence(_score);

        if (_timeSinceLastAdjustment < dynamicCooldown(_score)) return; 

        int rulesApplied = ApplyRules(GameplayValues.Values, _score);
        
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

    /// <summary>
    /// Calculates an adjusted score by applying the current pacing influence and difficulty multiplier to the specified base score.
    /// </summary>
    /// <remarks>The adjustment uses a linear interpolation between the base score and the base score
    /// multiplied by the current difficulty multiplier, weighted by the pacing influence. The returned value reflects
    /// dynamic game pacing and may change as pacing parameters are updated.</remarks>
    /// <param name="baseScore">The original score to be modified based on pacing and difficulty. Must be a finite, non-negative value.</param>
    /// <returns>A floating-point value representing the base score adjusted for pacing and difficulty. The result may be higher or lower than the input depending on the current pacing state.</returns>
    float ApplyPacingInfluence(float baseScore)
    {
        var _pacingMultiplier = Director.DifficultyMultiplier;
        var _adjustedScore = Mathf.Lerp(baseScore, baseScore * _pacingMultiplier, PacingInfluence);

        if (DebugMode)
        {
            Debug.Log($"[Pacing] Base Score: {baseScore:F2} -> Adjusted: {_adjustedScore:F2} " +
                     $"(Multiplier: {_pacingMultiplier:F2}, State: {Director.CurrentState})");
        }

        return _adjustedScore;
    }

    float dynamicCooldown(float score)
    {
        var baseCooldown = score < EasyThreshold ? AdjustmentCooldown * 0.5f : score > DifficultThreshold ? AdjustmentCooldown * 1.0f : AdjustmentCooldown;

        // Change cooldown based on pacing status
        if (EnablePacing)
        {
            baseCooldown *= Director.CurrentState switch
            {
                PacingDirector.PacingState.Peak => 0.7f,    // Più veloce durante picchi
                PacingDirector.PacingState.Relax => 2.0f,   // Più lento durante riposo
                _ => 1.0f
            };
        }

        return baseCooldown;
    }

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

        if (EnablePacing)
        {
            Debug.Log($"[Pacing] {Director.DebugInfo}");
        }
    }

    void OIDDAUpdate()
    {
        if (EnableSmoothing) _smoothingManager.SmoothUpdate(Time.DeltaTime);

        if (EnablePacing) Director.OnPacingDirectorUpdate(Time.DeltaTime, GameplayValues.Values);

        _timeSinceLastUpdate += Time.DeltaTime;
        _timeSinceLastAdjustment += Time.DeltaTime;

        if (InstantMetricsUpdated)
        {
            InstantMetricsUpdated = false; _timeSinceLastUpdate = 0f;
            return;
        }

        if (_timeSinceLastUpdate >= _updateInterval)
        {
            AnalyzeAndApply();
            _timeSinceLastUpdate -= _updateInterval;
        }
    }

    #region Pacing Director Agent Management

    public void AddPacingIntensity(float amount, string reason = "")
    {
        if (!EnablePacing) return;
        Director.AddIntensity(amount, reason);

        if (DebugMode)
        {
            Debug.Log($"[Pacing] Intensity added: + {amount} ({reason})");
        }
    }

    public bool IsShouldSpawnEncounter => EnablePacing ? Director.ShouldSpawnEncounter() : true;
    public PacingDirector.PacingState DirectorState => Director.CurrentState;
    public float PlayerStress => Director.StressLevel;
    public float PlayerFatigue => Director.FatigueLevel;

    #endregion

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
        if (_timerSender >= _delay)
        {
            AnalyzeAndApply();
            GameplayValues.SetValue(name, value);
            _timerSender = 0;
        }
    }

    T DelayReceiver<T>(string name)
    {
        _timerReceiver += Time.DeltaTime;
        if (_timerReceiver >= _delay)
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

    public void SetGlobal(string name, object value) => (_delay != 0f ? (Action)(() => DelaySender(name, value)) : () => GameplayValues.SetValue(name, value))();

    public void SetStaticGlobal(string NameAgent, object value) => (_delay != 0f ? (Action)(() => DelaySender(StaticORSDB[NameAgent].GlobalVariable, value)) : () => { AnalyzeAndApply(); GameplayValues.SetValue(StaticORSDB[NameAgent].GlobalVariable, value); })();

    public void QuickSender(string name, object value) { GameplayValues.SetValue(name, value); AnalyzeAndApply(); }

    public T GetGlobal<T>(string name) => (_delay != 0f) ? DelayReceiver<T>(name) : GameplayValues.GetValue(name) is T typeValue ? typeValue : default(T);

    public T GetStaticGlobal<T>(string NameAgent) => (_delay != 0f) ? DelayReceiver<T>(StaticORSDB[NameAgent].GlobalVariable) : GameplayValues.GetValue(StaticORSDB[NameAgent].GlobalVariable) is T typeValue ? typeValue : default(T);

    public T QuickReceiver<T>(string name) => GameplayValues.GetValue(name) is T typeValue ? typeValue : default(T);
    #endregion


    public override void OnUpdate()
    {
        OIDDAUpdate();
    }
}
