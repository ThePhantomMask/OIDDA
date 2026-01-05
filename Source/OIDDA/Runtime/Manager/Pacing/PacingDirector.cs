using System;
using System.Collections.Generic;
using FlaxEngine;
using FlaxEngine.Utilities;

namespace OIDDA;

/// <summary>
/// Psychological pacing system inspired by L4D's AI Director
/// </summary>
public class PacingDirector
{
    public enum PacingState
    {
        /// <summary>
        /// Tension build-up
        /// </summary>
        Build,
        /// <summary>
        /// Peak intensity
        /// </summary>
        Peak,
        /// <summary>
        /// Tension decrease
        /// </summary>
        Fade,
        /// <summary>
        /// Recovery/rest
        /// </summary>
        Relax
    }

    // Configuration
    public float IntensityDecayRate = 0.5f;
    public float IntensityBuildRate = 1.0f;
    public float PeakThreshold = 80f;
    public float RelaxThreshold = 20f;
    public float MinRelaxDuration = 10f;
    public float MaxPeakDuration = 30f;

    // Current state
    public PacingState CurrentState {  get; private set; }
    public float CurrentIntensity {  get; private set; }
    public float StateTimer { get; private set; }

    //  Historical data for analysis
    Queue<IntensityEvent> _intensityHistory = new(50);
    float _timeSinceLastPeak = 0f, _timeInCurrentState = 0f;

    // The psychological parameters of a player
    public float StressLevel { get; private set; }
    public float FatigueLevel { get; private set; }
    public float EngagementLevel { get; private set; }

    public PacingDirector()
    {
        CurrentState = PacingState.Build;
        CurrentIntensity = 0f;
    }

    /// <summary>
    /// Updates the pacing director's internal state and psychological metrics based on the elapsed time and current gameplay values.
    /// </summary>
    /// <param name="deltaTime">The amount of time, in seconds, that has elapsed since the last update. Must be non-negative.</param>
    /// <param name="GameplayValues">A dictionary containing current gameplay values that influence pacing and psychological metrics. Keys represent value names;
    /// values provide the corresponding data.</param>
    public void OnPacingDirectorUpdate(float deltaTime , Dictionary<string, object> GameplayValues)
    {
        _timeInCurrentState += deltaTime;
        _timeSinceLastPeak += deltaTime;
        StateTimer += deltaTime;

        UpdatePsychologicalMetrics(deltaTime, GameplayValues);
        UpdatePacingState(deltaTime);
        ApplyIntensityDecay(deltaTime);
        RecordIntensityEvent();
    }

    /// <summary>
    /// Increases the current intensity by the specified amount and records the change in the intensity history.
    /// </summary>
    /// <remarks>If the intensity history exceeds 50 entries, the oldest entry is removed. This method
    /// maintains a bounded history of recent intensity changes for tracking or auditing purposes.</remarks>
    /// <param name="amount">The amount by which to increase the current intensity. Can be negative to decrease intensity. The resulting
    /// intensity is clamped between 0 and 100.</param>
    /// <param name="reason">An optional description of the reason for the intensity change. This value is recorded in the intensity history.
    /// Can be null or empty.</param>
    public void AddIntensity(float amount, string reason = "")
    {
        var _oldIntensity = CurrentIntensity;
        CurrentIntensity = Mathf.Clamp(CurrentIntensity + amount, 0f, 100f);

        _intensityHistory.Enqueue(
           new IntensityEvent
           {
               Time = Time.GameTime,
               Intensity = CurrentIntensity,
               Delta = amount,
               Reason = reason
           }
        );

        if (_intensityHistory.Count > 50) _intensityHistory.Dequeue();
    }

    /// <summary>
    /// Updates the psychological metrics such as stress, fatigue, and engagement levels based on the elapsed time and provided contextual values.
    /// </summary>
    /// <remarks>This method adjusts internal psychological state variables according to the current pacing state and intensity. 
    /// The specific effects on each metric depend on the current state and may be influenced by the provided values. 
    /// Call this method regularly to ensure psychological metrics remain up to date with the simulation or game loop.</remarks>
    /// <param name="deltaTime">The amount of time, in seconds, since the last update. Must be a non-negative value.</param>
    /// <param name="values">A dictionary containing contextual values that may influence the update of psychological metrics. The expected
    /// keys and value types depend on the implementation context.</param>
    internal void UpdatePsychologicalMetrics(float deltaTime, Dictionary<string, object> values)
    {
        if (values == null || values.Count is 0) return;

        var _stressChange = CurrentState switch
        {
            PacingState.Build => deltaTime * 2f,
            PacingState.Peak => deltaTime * 5f,
            PacingState.Fade => -deltaTime * 1f,
            PacingState.Relax => -deltaTime * 3f,
            _ => 0f
        };

        StressLevel = Mathf.Clamp((StressLevel + _stressChange), 0f, 100f);

        var _fatigueChange = (CurrentState == PacingState.Relax) ? - deltaTime * 2f : deltaTime * 0.5f;
        FatigueLevel = Mathf.Clamp((FatigueLevel + _fatigueChange), 0f, 100f);

        EngagementLevel = (CurrentIntensity, StressLevel) switch
        {
            ( > 70f, _) and (_, < 80f) => Mathf.Lerp(EngagementLevel, 100f, deltaTime * 2f),
            ( < 20f, _) or (_, > 90f) => Mathf.Lerp(EngagementLevel, 30f, deltaTime),
            _ => Mathf.Lerp(EngagementLevel, 60f, deltaTime)
        };
    }

    /// <summary>
    /// Updates the pacing state based on the current intensity, fatigue, stress levels, and elapsed time.
    /// </summary>
    /// <remarks>This method should be called regularly, such as once per frame or update cycle, to ensure the pacing state transitions appropriately. 
    /// State transitions may trigger side effects such as invoking state changem events.</remarks>
    /// <param name="deltaTime">The time, in seconds, since the last update. Used to advance the pacing state logic.</param>
    internal void UpdatePacingState(float deltaTime)
    {
        PacingState _newState = CurrentState;

        switch(CurrentState)
        {
            case PacingState.Build:
                if (CurrentIntensity >= PeakThreshold)
                    _newState = PacingState.Peak;
                else if (FatigueLevel > 70f)
                    _newState = PacingState.Relax;
            break;

            case PacingState.Peak:
                if (_timeInCurrentState >= MaxPeakDuration || CurrentIntensity < (PeakThreshold * 0.8f))
                {
                    _newState = PacingState.Fade;
                    _timeSinceLastPeak = 0f;
                }
            break;

            case PacingState.Fade:
                if (CurrentIntensity <= RelaxThreshold)
                    _newState = PacingState.Relax;
            break;

            case PacingState.Relax:
                if (_timeInCurrentState >= MinRelaxDuration && FatigueLevel < 30f && StressLevel < 30f ||
                    _timeInCurrentState >= MinRelaxDuration * 2f) 
                    _newState = PacingState.Build;
            break;
        }

        if (_newState != CurrentState)
        {
            OnStateChanged(CurrentState, _newState);
            CurrentState = _newState;
            _timeInCurrentState = 0f;
        }
    }

    /// <summary>
    /// Applies a decay to the current intensity value based on the elapsed time and the current pacing state.
    /// </summary>
    /// <remarks>The rate of intensity decay varies depending on the current pacing state. 
    /// Calling this method repeatedly will gradually reduce the intensity to zero.</remarks>
    /// <param name="deltaTime">The time, in seconds, since the last update. Must be non-negative.</param>
    internal void ApplyIntensityDecay(float deltaTime)
    {
        var _decayRate = CurrentState switch
        {
            PacingState.Peak => IntensityDecayRate * 0.3f,
            PacingState.Fade => IntensityDecayRate * 2f,
            PacingState.Relax => IntensityDecayRate * 1.5f,
            _ => IntensityDecayRate
        };

        CurrentIntensity = Mathf.Max(0f, (CurrentIntensity - _decayRate * deltaTime));
    }

    /// <summary>
    /// Handles changes in the pacing state by responding to transitions between states.
    /// </summary>
    /// <param name="oldState">The previous pacing state before the transition occurred.</param>
    /// <param name="newState">The new pacing state after the transition.</param>
    void OnStateChanged(PacingState oldState, PacingState newState)
    {
        Debug.Log($"[Pacing] State: {oldState} -> {newState} (Intensity: {CurrentIntensity:F1}, Stress: {StressLevel:F1})");
    }

    /// <summary>
    /// Records the current intensity event and adds it to the intensity history if sufficient time has elapsed since the last event.
    /// </summary>
    /// <remarks>This method enforces a minimum interval of one second between recorded intensity events. 
    /// The intensity history is capped at 50 events; when this limit is exceeded, the oldest event is removed. 
    /// This helps maintain a recent history of intensity changes for further analysis or processing.</remarks>
    internal void RecordIntensityEvent()
    {
        if (_intensityHistory.Count > 0)
        {
            var lastEvent = _intensityHistory.ToArray()[_intensityHistory.Count - 1];
            if (Time.GameTime - lastEvent.Time < 1f) return;
        }

        _intensityHistory.Enqueue(new IntensityEvent
        {
            Time = Time.GameTime,
            Intensity = CurrentIntensity,
            Delta = 0f,
            Reason = $"State: {CurrentState}"
        });

        if (_intensityHistory.Count > 50) _intensityHistory.Dequeue();
    }

    /// <summary>
    /// Gets the current difficulty multiplier based on the pacing state and intensity.
    /// </summary>
    /// <remarks>The multiplier reflects the current challenge level, adjusting dynamically according to the
    /// pacing state and relevant parameters such as intensity and elapsed time. Typical values range from 0.5 during
    /// relaxed states up to 1.3 at peak intensity.</remarks>
    public float DifficultyMultiplier => CurrentState switch
    {
        PacingState.Build => Mathf.Lerp(0.8f, 1.0f, CurrentIntensity / PeakThreshold),
        PacingState.Peak => Mathf.Lerp(1.0f, 1.3f, _timeInCurrentState / MaxPeakDuration),
        PacingState.Fade => Mathf.Lerp(1.0f, 0.7f, _timeInCurrentState / 10f),
        PacingState.Relax => 0.5f,
        _ => 1.0f
    };

    /// <summary>
    /// Suggests whether to spawn enemies/events
    /// </summary>
    public bool ShouldSpawnEncounter()
    {
        if (CurrentState == PacingState.Relax) return false;
        if (CurrentState == PacingState.Peak && _timeInCurrentState < 5f) return false; // Don't overdo it
        if (FatigueLevel > 85f) return false; // Player too tired

        // Probability based on intensity and time since last peak
        float probability = CurrentState switch
        {
            PacingState.Build => Mathf.Saturate(CurrentIntensity / 100f),
            PacingState.Peak => 0.3f,
            PacingState.Fade => 0.1f,
            _ => 0f
        };

        // Increases probability if it has been a long time since the last peak
        if (_timeSinceLastPeak > 60f) probability *= 1.5f;

        return RandomUtil.Random.NextFloat(0f,1f) < probability;
    }

    /// <summary>
    /// Returns debug information
    /// </summary>
    public string DebugInfo =>
       $"State: {CurrentState} | Intensity: {CurrentIntensity:F1} | " +
       $"Stress: {StressLevel:F1} | Fatigue: {FatigueLevel:F1} | " +
       $"Engagement: {EngagementLevel:F1} | Time in State: {_timeInCurrentState:F1}s";

    /// <summary>
    /// Calculates the average intensity of events that occurred within the specified number of seconds before the current game time.
    /// </summary>
    /// <param name="seconds">The time window, in seconds, over which to calculate the average intensity. Must be greater than zero. The
    /// default is 30 seconds.</param>
    /// <returns>The average intensity of events within the specified time window. Returns 0 if there are no recorded events, or the current intensity if no events occurred within the time window.</returns>
    public float AverageIntensity(float seconds = 30f)
    {
        if (_intensityHistory.Count == 0) return 0f;

        var _cutOffTime = Time.GameTime - seconds;
        var _recentEvents = new List<IntensityEvent>();

        _intensityHistory.ForEach( evt =>
        {
            if (evt.Time >= _cutOffTime) _recentEvents.Add(evt);
        });

        if (_recentEvents.Count == 0) return CurrentIntensity;

        float sum = 0;
        _recentEvents.ForEach(evt => sum += evt.Intensity);

        return sum / _recentEvents.Count;
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
    public PacingStatistics Statistics => new PacingStatistics
    {
        CurrentIntensity = CurrentIntensity,
        AverageIntensity30s = AverageIntensity(),
        StressLevel = StressLevel,
        FatigueLevel = FatigueLevel,
        EngagementLevel = EngagementLevel,
        CurrentState = CurrentState,
        TimeInCurrentState = _timeInCurrentState,
        IsInFlowState = IsInFlowState,
        EventCount = _intensityHistory.Count
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
/// Pacing statistics for analysis and debugging
/// </summary>
public struct PacingStatistics
{
    public float CurrentIntensity;
    public float AverageIntensity30s;
    public float StressLevel;
    public float FatigueLevel;
    public float EngagementLevel;
    public PacingDirector.PacingState CurrentState;
    public float TimeInCurrentState;
    public float TimeSinceLastPeak;
    public bool IsInFlowState;
    public int EventCount;
}
