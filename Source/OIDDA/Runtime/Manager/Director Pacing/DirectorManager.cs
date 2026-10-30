using FlaxEngine;
using FlaxEngine.Utilities;
using OIDDA.Data;
using System.Collections.Generic;

namespace OIDDA;

/// <summary>
/// Psychological pacing system inspired by L4D's The Director
/// </summary>
public class DirectorManager
{
    // Configuration
    public float IntensityDecayRate = 0.5f;
    public float PeakThreshold = 80f;
    public float RelaxThreshold = 20f;
    public float MinRelaxDuration = 10f;
    public float MaxPeakDuration = 30f;

    [Tooltip("Seconds between Director evaluations (metrics, rules, pacing). 0 = every frame.")]
    public float UpdateInterval = 0f;

    [Tooltip("Stress gained per second when the overall score is at its maximum (and lost at its minimum). A neutral score of 0.5 changes nothing.")]
    public float StressRiseRate = 5f;
    [Tooltip("Stress lost per second regardless of difficulty, so it settles back on its own.")]
    public float StressDecayRate = 0.5f;
    [Tooltip("Fatigue gained per second while in Build or Peak.")]
    public float FatigueGainRate = 0.5f;
    [Tooltip("Fatigue lost per second while in Relax.")]
    public float FatigueRecoveryRate = 2f;
    [HideInEditor] public bool isDirectorSmoothing;
    [HideInEditor] public bool DebugMode;
    [HideInEditor] public OIDDAConfig currentConfig;

    // Current state
    public DirectorState CurrentState {  get; private set; }
    public float CurrentIntensity {  get; private set; }
    /// <summary> Seconds spent in the current pacing state. </summary>
    public float StateTimer => timeInCurrentState;

    //  Historical data for analysis
    const int MaxHistory = 50;
    const float FadeBlendSeconds = 10f;
    Queue<IntensityEvent> intensityHistory = new(MaxHistory);
    float lastHistoryTime;
    float intervalAccumulator;
    float timeSinceLastPeak = 0f, timeInCurrentState = 0f;
    float stressRate => StressLevel * 0.01f; float fatigueRate => FatigueLevel * 0.01f;
    SmoothingManager smoothingManager = new();
    IValueStore _store;

    // The psychological parameters of a player
    public float StressLevel { get; private set; }
    public float FatigueLevel { get; private set; }
    public float EngagementLevel { get; private set; }

    /// <summary>
    /// Copies the tunable settings (not the runtime state) from another Director. Used to give every player's personal
    /// Director the same settings as the one configured on the manager.
    /// </summary>
    public void CopySettingsFrom(DirectorManager other)
    {
        IntensityDecayRate = other.IntensityDecayRate;
        PeakThreshold = other.PeakThreshold;
        RelaxThreshold = other.RelaxThreshold;
        MinRelaxDuration = other.MinRelaxDuration;
        MaxPeakDuration = other.MaxPeakDuration;
        UpdateInterval = other.UpdateInterval;
        StressRiseRate = other.StressRiseRate;
        StressDecayRate = other.StressDecayRate;
        FatigueGainRate = other.FatigueGainRate;
        FatigueRecoveryRate = other.FatigueRecoveryRate;
    }

    public DirectorManager()
    {
        CurrentState = DirectorState.Build;
        CurrentIntensity = 0f;
    }

    /// <summary>
    /// Updates the pacing director's internal state and psychological metrics based on the elapsed time and current gameplay values.
    /// </summary>
    /// <param name="deltaTime">The amount of time, in seconds, that has elapsed since the last update. Must be non-negative.</param>
    /// <param name="GameplayValues">A dictionary containing current gameplay values that influence pacing and psychological metrics. Keys represent value names;
    /// values provide the corresponding data.</param>
    public void OnDirectorUpdate(float deltaTime, GameplayGlobals GameplayValues)
    {
        if (!GameplayValues)
            return;

        OnDirectorUpdate(deltaTime, new GameplayGlobalsStore(GameplayValues));
    }

    /// <summary>
    /// Same as <see cref="OnDirectorUpdate(float, GameplayGlobals)"/>, reading and writing through any <see cref="IValueStore"/>.
    /// </summary>
    public void OnDirectorUpdate(float deltaTime, IValueStore store)
    {
        if (store == null || !store.IsValid)
            return;

        _store = store;
        smoothingManager.Store = store;

        timeInCurrentState += deltaTime;
        timeSinceLastPeak += deltaTime;

        smoothingManager.SmoothUpdate(deltaTime);

        // Smoothing and timers run every frame; the heavier evaluation can be throttled.
        if (UpdateInterval > 0f)
        {
            intervalAccumulator += deltaTime;
            if (intervalAccumulator < UpdateInterval) return;

            deltaTime = intervalAccumulator;
            intervalAccumulator = 0f;
        }

        UpdatePsychologicalMetrics(deltaTime, store.Values);
        UpdatePacingState();
        ApplyIntensityDecay(deltaTime);
        RecordIntensityEvent();
    }

    /// <summary>
    /// Increases the current intensity by the specified amount and records the change in the intensity history.
    /// </summary>
    /// <remarks>If the intensity history exceeds the maximum of 50 entries, the oldest entry is removed. This method
    /// maintains a bounded history of recent intensity changes for tracking or auditing purposes.</remarks>
    /// <param name="amount">The amount by which to increase the current intensity. Can be negative to decrease intensity. The resulting
    /// intensity is clamped between 0 and 100.</param>
    /// <param name="reason">An optional description of the reason for the intensity change. This value is recorded in the intensity history.
    /// Can be null or empty.</param>
    public void AddIntensity(float amount, string reason = "")
    {
        var oldIntensity = CurrentIntensity;
        CurrentIntensity = Mathf.Clamp(oldIntensity + amount, 0f, 100f);

        PushHistory(new IntensityEvent
        {
            Time = Time.GameTime,
            Intensity = CurrentIntensity,
            Delta = amount,
            Reason = reason
        });
    }

    void PushHistory(IntensityEvent evt)
    {
        intensityHistory.Enqueue(evt);
        lastHistoryTime = evt.Time;

        if (intensityHistory.Count > MaxHistory) intensityHistory.Dequeue();
    }

    // Extra intensity that belongs to the group itself (an event that affects everyone). Decays like normal intensity.
    float _groupBoost;

    /// <summary>
    /// Group Director only: adds intensity on top of the aggregated players' intensity.
    /// </summary>
    public void AddGroupBoost(float amount, string reason = "")
    {
        _groupBoost = Mathf.Clamp(_groupBoost + amount, 0f, 100f);

        PushHistory(new IntensityEvent
        {
            Time = Time.GameTime,
            Intensity = Mathf.Clamp(CurrentIntensity + amount, 0f, 100f),
            Delta = amount,
            Reason = reason
        });
    }

    /// <summary>
    /// Group Director update: instead of building up its own intensity, stress and fatigue, it takes the values aggregated
    /// from the players. The pacing state machine, the rules and the smoothing work as for a single player.
    /// </summary>
    public void OnGroupUpdate(float deltaTime, IValueStore store, GroupInputs inputs)
    {
        if (store == null || !store.IsValid)
            return;

        smoothingManager.Store = _store = store;

        timeInCurrentState += deltaTime;
        timeSinceLastPeak += deltaTime;

        smoothingManager.SmoothUpdate(deltaTime);

        if (UpdateInterval > 0f)
        {
            intervalAccumulator += deltaTime;
            if (intervalAccumulator < UpdateInterval) return;

            deltaTime = intervalAccumulator;
            intervalAccumulator = 0f;
        }

        _groupBoost = Mathf.Max(0f, _groupBoost - CurrentDecayRate * deltaTime);

        StressLevel = Mathf.Clamp(inputs.Stress, 0f, 100f);
        FatigueLevel = Mathf.Clamp(inputs.Fatigue, 0f, 100f);
        CurrentIntensity = Mathf.Clamp(inputs.Intensity + _groupBoost, 0f, 100f);

        if (currentConfig != null && currentConfig.DirectorRules != null && currentConfig.DirectorRules.Count > 0)
            ApplyRules(store.Values, inputs.Score, deltaTime);

        UpdateEngagement(deltaTime);
        UpdatePacingState();
        RecordIntensityEvent();
    }

    /// <summary>
    /// Updates the psychological metrics such as stress, fatigue, and engagement levels based on the elapsed time and provided contextual values.
    /// </summary>
    /// <remarks>This method adjusts internal psychological state variables according to the current pacing state and intensity. 
    /// The specific effects on each metric depend on the current state and may be influenced by the provided values. 
    /// Call this method regularly to ensure psychological metrics remain up to date with the simulation or game loop.</remarks>
    /// <param name="deltaTime">The amount of time, in seconds, since the last update. Must be a non-negative value.</param>
    /// <param name="values">A dictionary containing contextual values that may influence the update of psychological metrics. 
    /// The expected keys and value types depend on the implementation context.</param>
    internal void UpdatePsychologicalMetrics(float deltaTime, Dictionary<string, object> values)
    {
        bool canScore = values != null && values.Count > 0 &&
                        currentConfig != null && currentConfig.Metrics != null && currentConfig.Metrics.Count > 0;

        if (canScore)
        {
            // Overall score: 0 = very easy, 1 = very difficult. Also drives the Director rules.
            var score = CalculateScoreByData(values, deltaTime);
            UpdateStress(score, deltaTime);
        }
        else
        {
            UpdateStress(0.5f, deltaTime); // neutral: only the natural decay applies
        }

        UpdateFatigue(deltaTime);
        UpdateEngagement(deltaTime);
    }

    void UpdateEngagement(float deltaTime)
    {
        EngagementLevel = (CurrentIntensity, StressLevel) switch
        {
            ( > 70f, _) and (_, < 80f) => Mathf.Lerp(EngagementLevel, 100f, deltaTime * 2f),
            ( < 20f, _) or (_, > 90f) => Mathf.Lerp(EngagementLevel, 30f, deltaTime),
            _ => Mathf.Lerp(EngagementLevel, 60f, deltaTime)
        };
    }

    /// <summary>
    /// Stress rises while the overall score indicates difficulty, falls while it indicates the game is easy,
    /// and always decays slowly on its own. Scaled by time, so it does not depend on the frame rate.
    /// </summary>
    void UpdateStress(float overallScore, float deltaTime)
    {
        var load = (overallScore - 0.5f) * 2f; // -1 (very easy) .. +1 (very difficult)
        var change = (load * StressRiseRate - StressDecayRate) * deltaTime;
        StressLevel = Mathf.Clamp(StressLevel + change, 0f, 100f);
    }

    /// <summary>
    /// Fatigue builds up slowly in Build/Peak, recovers in Relax and holds during Fade.
    /// </summary>
    void UpdateFatigue(float deltaTime)
    {
        var rate = CurrentState switch
        {
            DirectorState.Build or DirectorState.Peak => FatigueGainRate,
            DirectorState.Relax => -FatigueRecoveryRate,
            _ => 0f
        };

        FatigueLevel = Mathf.Clamp(FatigueLevel + rate * deltaTime, 0f, 100f);
    }

    /// <summary>
    /// Updates the pacing state based on the current intensity, fatigue, stress levels, and elapsed time.
    /// </summary>
    /// <remarks> This method should be called regularly, such as once per frame or update cycle, to ensure the pacing state transitions appropriately. 
    /// State transitions may trigger side effects such as invoking state changem events.</remarks>
    internal void UpdatePacingState()
    {
        DirectorState newState = CurrentState;

        switch(CurrentState)
        {
            case DirectorState.Build:
                if (CurrentIntensity >= PeakThreshold)
                    newState = DirectorState.Peak;
                else if (FatigueLevel > 70f)
                    newState = DirectorState.Relax;
            break;

            case DirectorState.Peak:
                if (timeInCurrentState >= MaxPeakDuration || CurrentIntensity < (PeakThreshold * 0.8f))
                {
                    newState = DirectorState.Fade;
                    timeSinceLastPeak = 0f;
                }
            break;

            case DirectorState.Fade:
                if (CurrentIntensity <= RelaxThreshold)
                    newState = DirectorState.Relax;
            break;

            case DirectorState.Relax:
                if (timeInCurrentState >= MinRelaxDuration && FatigueLevel < 30f && StressLevel < 30f ||
                    timeInCurrentState >= MinRelaxDuration * 2f) 
                    newState = DirectorState.Build;
            break;
        }

        if (newState != CurrentState)
        {
            var oldState = CurrentState;
            CurrentState = newState;
            timeInCurrentState = 0f;
            OnStateChanged(oldState, newState);
        }
    }

    /// <summary>
    /// Applies a decay to the current intensity value based on the elapsed time and the current pacing state.
    /// </summary>
    /// <remarks>The rate of intensity decay varies depending on the current pacing state. 
    /// Calling this method repeatedly will gradually reduce the intensity to zero.</remarks>
    float CurrentDecayRate => CurrentState switch
    {
        DirectorState.Peak => IntensityDecayRate * 0.3f,
        DirectorState.Fade => IntensityDecayRate * 2f,
        DirectorState.Relax => IntensityDecayRate * 1.5f,
        _ => IntensityDecayRate
    };

    internal void ApplyIntensityDecay(float deltaTime)
    {
        CurrentIntensity = Mathf.Max(0f, (CurrentIntensity - CurrentDecayRate * deltaTime));
    }

    /// <summary>
    /// Handles changes in the pacing state by responding to transitions between states.
    /// </summary>
    /// <param name="oldState">The previous pacing state before the transition occurred.</param>
    /// <param name="newState">The new pacing state after the transition.</param>
    void OnStateChanged(DirectorState oldState, DirectorState newState)
    {
        if (DebugMode)
            Debug.Log($"[Director] State: {oldState} -> {newState} (Intensity: {CurrentIntensity:F1}, Stress: {StressLevel:F1})");
    }

    /// <summary>
    /// Stops every smoothing currently in progress.
    /// </summary>
    public void ResetSmoothing() => smoothingManager.Clear();

    /// <summary>
    /// Records the current intensity event and adds it to the intensity history if sufficient time has elapsed since the last event.
    /// </summary>
    /// <remarks>This method enforces a minimum interval of one second between recorded intensity events. 
    /// The intensity history is capped at 50 events; when this limit is exceeded, the oldest event is removed. 
    /// This helps maintain a recent history of intensity changes for further analysis or processing.</remarks>
    internal void RecordIntensityEvent()
    {
        if (intensityHistory.Count > 0 && Time.GameTime - lastHistoryTime < 1f) return;

        PushHistory(new IntensityEvent
        {
            Time = Time.GameTime,
            Intensity = CurrentIntensity,
            Delta = 0f,
            Reason = $"State: {CurrentState}"
        });
    }

    protected float CalculateScoreByData(Dictionary<string,object> values, float deltatime)
    {
        var score = MetricsAggregator.CalculateOverallScore(currentConfig.Metrics, values);
        if (currentConfig.DirectorRules != null && currentConfig.DirectorRules.Count > 0)
            ApplyRules(values, score, deltatime);
        return score;
    }

    protected int ApplyRules(Dictionary<string, object> currentValues, float overallScore, float deltaTime)
    {
        var easy = Mathf.Min(stressRate, fatigueRate);
        var difficult = Mathf.Max(stressRate, fatigueRate);

        int rulesApplied = 0;
        foreach (var rule in currentConfig.DirectorRules)
        {
            if (rule == null) continue;

            var score = overallScore + CalculateScoreByEmotion(rule, deltaTime);
            var ctx = new DifficultyContext(score, easy, difficult);

            if (RuleApplier.ApplyRule(rule, currentValues, ctx, isDirectorSmoothing, _store, smoothingManager, currentConfig.SmoothingSpeed, false))
                rulesApplied++;
        }
        return rulesApplied;
    }

    float CalculateScoreByEmotion(DirectorRule rule, float deltaTime) => rule.Emotion switch
    {
        EmotionType.Stress => CurrentState switch
        {
            DirectorState.Build => deltaTime * 2f,
            DirectorState.Peak => deltaTime * 5f,
            DirectorState.Fade => -deltaTime * 1f,
            DirectorState.Relax => -deltaTime * 3f,
            _ => 0f
        },
        EmotionType.Fatigue => (CurrentState == DirectorState.Relax) ? -deltaTime * 2f : deltaTime * 0.5f,
        _ => 0f
    };

    /// <summary>
    /// Gets the current difficulty multiplier based on the pacing state and intensity.
    /// </summary>
    /// <remarks>The multiplier reflects the current challenge level, adjusting dynamically according to the
    /// pacing state and relevant parameters such as intensity and elapsed time. 
    /// Typical values range from 0.5 during relaxed states up to 1.3 at peak intensity.</remarks>
    public float DifficultyMultiplier => CurrentState switch
    {
        DirectorState.Build => Mathf.Lerp(0.8f, 1.0f, CurrentIntensity / PeakThreshold),
        DirectorState.Peak => Mathf.Lerp(1.0f, 1.3f, timeInCurrentState / MaxPeakDuration),
        DirectorState.Fade => Mathf.Lerp(1.0f, 0.7f, Mathf.Saturate(timeInCurrentState / FadeBlendSeconds)),
        DirectorState.Relax => 0.5f,
        _ => 1.0f
    };

    /// <summary>
    /// Suggests whether to spawn enemies/events
    /// </summary>
    public bool ShouldSpawnEncounter()
    {
        if (CurrentState == DirectorState.Relax ||
            CurrentState == DirectorState.Peak && timeInCurrentState < 5f || 
            FatigueLevel > 85f) return false;

        // Probability based on intensity and time since last peak
        float probability = CurrentState switch
        {
            DirectorState.Build => Mathf.Saturate(CurrentIntensity / 100f),
            DirectorState.Peak => 0.3f,
            DirectorState.Fade => 0.1f,
            _ => 0f
        };

        // Increases probability if it has been a long time since the last peak
        if (timeSinceLastPeak > 60f) probability *= 1.5f;

        return RandomUtil.Random.NextFloat() < probability;
    }

    /// <summary>
    /// Returns debug information
    /// </summary>
    public string DebugInfo =>
       $"State: {CurrentState} | Intensity: {CurrentIntensity:F1} | " +
       $"Stress: {StressLevel:F1} | Fatigue: {FatigueLevel:F1} | " +
       $"Engagement: {EngagementLevel:F1} | Time in State: {timeInCurrentState:F1}s";

    /// <summary>
    /// Calculates the average intensity of events that occurred within the specified number of seconds before the current game time.
    /// </summary>
    /// <param name="seconds">The time window, in seconds, over which to calculate the average intensity. Must be greater than zero. The
    /// default is 30 seconds.</param>
    /// <returns>The average intensity of events within the specified time window. Returns 0 if there are no recorded events, or the current intensity if no events occurred within the time window.</returns>
    public float AverageIntensity(float seconds = 30f)
    {
        if (intensityHistory.Count == 0) return 0f;

        var cutOffTime = Time.GameTime - seconds;
        float sum = 0f;
        int count = 0;

        foreach (var evt in intensityHistory)
        {
            if (evt.Time < cutOffTime) continue;
            sum += evt.Intensity;
            count++;
        }

        return count == 0 ? CurrentIntensity : sum / count;
    }

    /// <summary>
    /// Gets a value indicating whether the current state meets the criteria for being considered "in flow."
    /// </summary>
    public bool IsInFlowState => EngagementLevel > 70f && StressLevel < 70f && FatigueLevel < 60f;

    /// <summary>
    /// Gets the current pacing statistics, including intensity, stress, fatigue, engagement, and flow state information.
    /// </summary>
    /// <remarks>The returned statistics provide a snapshot of the current pacing metrics, which can be used
    /// to monitor and analyze user performance or engagement over time. The values reflect the most recent state and
    /// are updated each time the property is accessed.</remarks>
    public DirectorStatistics Statistics => new DirectorStatistics
    {
        CurrentIntensity = CurrentIntensity,
        AverageIntensity30s = AverageIntensity(),
        StressLevel = StressLevel,
        FatigueLevel = FatigueLevel,
        EngagementLevel = EngagementLevel,
        CurrentState = CurrentState,
        TimeInCurrentState = timeInCurrentState,
        TimeSinceLastPeak = timeSinceLastPeak,
        IsInFlowState = IsInFlowState,
        EventCount = intensityHistory.Count
    };
}

/// <summary>
/// Intensity event for historical tracking
/// </summary>
public struct IntensityEvent
{
    public float Time;
    public float Intensity;
    public float Delta;
    public string Reason;
}

/// <summary>
/// Director statistics for analysis and debugging
/// </summary>
public struct DirectorStatistics
{
    public float CurrentIntensity;
    public float AverageIntensity30s;
    public float StressLevel;
    public float FatigueLevel;
    public float EngagementLevel;
    public DirectorState CurrentState;
    public float TimeInCurrentState;
    public float TimeSinceLastPeak;
    public bool IsInFlowState;
    public int EventCount;
}
