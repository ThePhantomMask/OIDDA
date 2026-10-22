using FlaxEngine;
using OIDDA.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OIDDA;

/// <summary>
/// Everything OIDDA keeps for one player: the value store, smoothing, the personal Director, the adjustment timers
/// and the delay state. Single-player uses one default context, owned by <see cref="OIDDAManager"/>.
/// Thresholds, cooldowns and debug flags are manager-level settings, shared by every context.
/// </summary>
public sealed class OIDDAContext
{
    readonly OIDDAManager _owner;
    readonly SmoothingManager _smoothing = new();
    DirectorManager _director;

    bool _isUseSmoothing, _isUseDirector, _isAnalyzing;
    float _timeSinceLastUpdate, _timeSinceLastAdjustment;

    // Delay (per variable): last send/receive time, the newest value waiting to be sent, and the last value read.
    readonly Dictionary<string, float> _lastSendTime = new(), _lastReceiveTime = new();
    readonly Dictionary<string, object> _pendingSends = new(), _receiveCache = new();
    readonly List<string> _flushKeys = new();

    internal OIDDAContext(OIDDAManager owner, DirectorManager director)
    {
        _owner = owner;
        _director = director;
    }

    /// <summary> Where this context reads and writes its gameplay values. </summary>
    public IValueStore Store { get; private set; }

    public bool HasStore => Store != null && Store.IsValid;

    public DirectorManager Director => _director;

    public bool IsUseSmoothing => _isUseSmoothing;

    public bool IsUseDirector => _isUseDirector;

    #region Lifecycle

    // The manager's Director field can be replaced when the scene is deserialized, so it is re-bound on every initialization.
    internal void BindDirector(DirectorManager director) => _director = director;

    internal void SetStore(IValueStore store)
    {
        Store = store;
        _smoothing.Store = store;
    }

    internal void ApplyConfig(OIDDAConfig config)
    {
        _director.currentConfig = config;
        _isUseSmoothing = config.UseDDASmoothing;
        _director.isDirectorSmoothing = config.OverrideDirectorSmoothing ? config.UseDirectorSmoothing : _isUseSmoothing;
        _isUseDirector = config.UseDirector;
    }

    internal void ResetTimers() => _timeSinceLastUpdate = _timeSinceLastAdjustment = 0f;

    internal void Reset()
    {
        if (HasStore) Store.ResetValues();
        _smoothing.Clear();
        _director.ResetSmoothing();
        _lastSendTime.Clear();
        _lastReceiveTime.Clear();
        _pendingSends.Clear();
        _receiveCache.Clear();
    }

    internal void Update(float deltaTime)
    {
        _director.DebugMode = _owner.DebugMode;

        if (_owner.Delay != 0f) FlushPendingSends();
        if (_isUseSmoothing) _smoothing.SmoothUpdate(deltaTime);
        if (_isUseDirector) _director.OnDirectorUpdate(deltaTime, Store);
        _timeSinceLastUpdate += deltaTime;
        _timeSinceLastAdjustment += deltaTime;

        if (_timeSinceLastUpdate >= _owner.UpdateInterval)
        {
            AnalyzeAndApply();
            _timeSinceLastUpdate -= _owner.UpdateInterval;
        }
    }

    #endregion

    #region Analysis

    internal void AnalyzeAndApply()
    {
        // Safety net against re-entrancy (e.g. user code calling QuickSender from a rule callback).
        if (_isAnalyzing) return;

        _isAnalyzing = true;
        try { AnalyzeAndApplyInternal(); }
        finally { _isAnalyzing = false; }
    }

    void AnalyzeAndApplyInternal()
    {
        var config = _director.currentConfig;
        if (config == null || config.Rules == null || config.Metrics == null ||
            config.Rules.Count == 0 || config.Metrics.Count == 0) return;

        // Cheap early exit: nothing below can use a cooldown shorter than the smallest multiplier.
        if (_timeSinceLastAdjustment < _owner.AdjustmentCooldown * MinCooldownFactor) return;

        // GameplayGlobals.Values can be a marshalled copy: read it once per cycle.
        var values = Store.Values;
        float score;

        if (_owner.DebugMode)
        {
            var analysis = MetricsAggregator.Analyze(config.Metrics, values, _owner.EasyThreshold, _owner.DifficultThreshold);
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

            if (_owner.DebugMode && _isUseSmoothing && _smoothing.HasActiveSmoothings)
                Debug.Log($"[OIDDA] Smoothing {_smoothing.ActiveSmoothingCount} value(s)");
        }
    }

    /// <summary>
    /// Calculates an adjusted score by applying the current pacing influence and difficulty multiplier to the specified base score.
    /// </summary>
    /// <remarks>The adjustment uses a linear interpolation between the base score and the base score
    /// multiplied by the current difficulty multiplier, weighted by the pacing influence. The returned value reflects
    /// dynamic game pacing and may change as pacing parameters are updated.</remarks>
    float ApplyDirectorInfluence(float baseScore)
    {
        var pacingMultiplier = _director.DifficultyMultiplier;
        var adjustedScore = Mathf.Lerp(baseScore, baseScore * pacingMultiplier, _owner.DirectorInfluence);

        if (_owner.DebugMode)
        {
            Debug.Log($"[Director] Base Score: {baseScore:F2} -> Adjusted: {adjustedScore:F2} " +
                     $"(Multiplier: {pacingMultiplier:F2}, State: {_director.CurrentState})");
        }

        return adjustedScore;
    }

    const float EasyCooldownFactor = 0.5f, PeakCooldownFactor = 0.7f, RelaxCooldownFactor = 2.0f;

    // Smallest multiplier dynamicCooldown can return (easy score, and Peak when the Director is on).
    float MinCooldownFactor => _isUseDirector ? EasyCooldownFactor * PeakCooldownFactor : EasyCooldownFactor;

    float dynamicCooldown(float score)
    {
        var baseCooldown = score < _owner.EasyThreshold ? _owner.AdjustmentCooldown * EasyCooldownFactor : _owner.AdjustmentCooldown;

        // Change cooldown based on director status
        if (_isUseDirector)
        {
            baseCooldown *= _director.CurrentState switch
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
        var config = _director.currentConfig;
        var ctx = new DifficultyContext(overallScore, _owner.EasyThreshold, _owner.DifficultThreshold);
        int rulesApplied = 0;

        foreach (var rule in config.Rules)
        {
            if (rule == null) continue;
            if (RuleApplier.ApplyRule(rule, currentValues, ctx, _isUseSmoothing, Store, _smoothing, config.SmoothingSpeed, _owner.DebugMode))
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

        var problematic = analysis.MetricInfos.Where(info => info.NormalizedScore > _owner.DifficultThreshold)
                                              .OrderByDescending(info => info.NormalizedScore)
                                              .ToList();
        if (problematic.Count > 0)
        {
            analysisLog.AppendLine($"Problematic Metrics ({problematic.Count}):");
            problematic.ForEach(metric => analysisLog.AppendLine($"{metric.MetricName}: {metric.NormalizedScore:F3}"));
        }

        if (_isUseDirector) analysisLog.AppendLine($"[Director] {_director.DebugInfo}");

        Debug.Log(analysisLog.ToString());
    }

    #endregion

    #region Director

    public void AddDirectorIntensity(float amount, string reason = "")
    {
        if (!_isUseDirector) return;
        _director.AddIntensity(amount, reason);

        if (_owner.DebugMode) Debug.Log($"[Director] Intensity added: + {amount} ({reason})");
    }

    public bool IsShouldSpawnEncounter => _isUseDirector ? _director.ShouldSpawnEncounter() : true;
    public DirectorState DirectorState => _director.CurrentState;
    public float Intensity => _director.CurrentIntensity;
    public float PlayerStress => _director.StressLevel;
    public float PlayerFatigue => _director.FatigueLevel;

    #endregion

    #region Values

    // Each variable has its own clock (game time, so it does not depend on how many calls are made per frame).
    // The first value goes out immediately; later ones are limited to one write per delay, keeping the newest
    // value instead of dropping it.
    void DelaySender(string name, object value)
    {
        var now = Time.GameTime;

        if (!_lastSendTime.TryGetValue(name, out var last) || now - last >= _owner.Delay)
        {
            _pendingSends.Remove(name);
            SendNow(name, value, now);
            return;
        }

        _pendingSends[name] = value;
    }

    void SendNow(string name, object value, float now)
    {
        _lastSendTime[name] = now;
        AnalyzeAndApply();
        Store.SetValue(name, value);
    }

    void FlushPendingSends()
    {
        if (_pendingSends.Count == 0) return;

        var now = Time.GameTime;
        _flushKeys.Clear();
        foreach (var pending in _pendingSends)
            if (now - _lastSendTime[pending.Key] >= _owner.Delay) _flushKeys.Add(pending.Key);

        foreach (var key in _flushKeys)
        {
            var value = _pendingSends[key];
            _pendingSends.Remove(key);
            SendNow(key, value, now);
        }
    }

    // Reads the real value at most once per delay; in between, the last valid value is returned (never default).
    T DelayReceiver<T>(string name)
    {
        var now = Time.GameTime;

        if (_lastReceiveTime.TryGetValue(name, out var last) && now - last < _owner.Delay &&
            _receiveCache.TryGetValue(name, out var cached) && cached is T cachedValue)
            return cachedValue;

        var value = Store.GetValue<T>(name);
        _receiveCache[name] = value;
        _lastReceiveTime[name] = now;
        return value;
    }

    public void SetGlobal(string name, object value)
    {
        if (_owner.Delay != 0f) DelaySender(name, value);
        else Store.SetValue(name, value);
    }

    /// <summary>
    /// Static ORS variant of <see cref="SetGlobal"/>: without a delay it runs an analysis before writing the value.
    /// </summary>
    public void SetStaticGlobal(string variable, object value)
    {
        if (_owner.Delay != 0f)
        {
            DelaySender(variable, value);
            return;
        }

        AnalyzeAndApply();
        Store.SetValue(variable, value);
    }

    public void QuickSender(string name, object value) { Store.SetValue(name, value); AnalyzeAndApply(); }

    /// <summary>
    /// Writes a value without triggering an analysis (internal use by rules and smoothing).
    /// </summary>
    internal void WriteGlobal(string name, object value) => Store.SetValue(name, value);

    public T GetGlobal<T>(string name) => (_owner.Delay != 0f) ? DelayReceiver<T>(name) : Store.GetValue<T>(name);

    public T QuickReceiver<T>(string name) => Store.GetValue<T>(name);

    #endregion
}
