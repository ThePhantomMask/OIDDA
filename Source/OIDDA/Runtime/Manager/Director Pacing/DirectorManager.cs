using FlaxEngine;
using FlaxEngine.Utilities;
using Newtonsoft.Json.Linq;
using OIDDA.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OIDDA;

/// <summary>
/// Psychological pacing system inspired by L4D's The Director
/// </summary>
public class DirectorManager
{
    // Configuration
    public float IntensityDecayRate = 0.5f;
    public float IntensityBuildRate = 1.0f;
    public float PeakThreshold = 80f;
    public float RelaxThreshold = 20f;
    public float MinRelaxDuration = 10f;
    public float MaxPeakDuration = 30f;

    // Current state
    public DirectorState CurrentState {  get; private set; }
    public float CurrentIntensity {  get; private set; }
    public float StateTimer { get; private set; }

    //  Historical data for analysis
    Queue<IntensityEvent> intensityHistory = new(50);
    float timeSinceLastPeak = 0f, timeInCurrentState = 0f;

    // The psychological parameters of a player
    public float StressLevel { get; private set; }
    public float FatigueLevel { get; private set; }
    public float EngagementLevel { get; private set; }

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
    public void OnDirectorUpdate(float deltaTime , Dictionary<string, DirectorValue> GameplayValues)
    {
        timeInCurrentState += deltaTime;
        timeSinceLastPeak += deltaTime;
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
        var oldIntensity = CurrentIntensity;
        CurrentIntensity = Mathf.Clamp(CurrentIntensity + amount, 0f, 100f);

        intensityHistory.Enqueue(
           new IntensityEvent
           {
               Time = Time.GameTime,
               Intensity = CurrentIntensity,
               Delta = amount,
               Reason = reason
           }
        );

        if (intensityHistory.Count > 50) intensityHistory.Dequeue();
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
    internal void UpdatePsychologicalMetrics(float deltaTime, Dictionary<string, DirectorValue> values)
    {
        if (values == null || values.Count is 0) return;

        foreach (var value in values)
        {
            var resultScore = DirectorUtils.CalculateScoreByDirectorValue(value.Value);

            switch (value.Value.Category)
            {
                case DirectorCategory.Stress:
                  var stressChange = CurrentState switch
                  {
                        DirectorState.Build => deltaTime * 2f,
                        DirectorState.Peak => deltaTime * 5f,
                        DirectorState.Fade => -deltaTime * 1f,
                        DirectorState.Relax => -deltaTime * 3f,
                        _ => 0f
                  };
                  Debug.Write(LogType.Info, $"{value.Key} value {resultScore} will be applied to Stress Level");
                  StressLevel = Mathf.Clamp((StressLevel + (stressChange * resultScore)), 0f, 100f);
                break;
                case DirectorCategory.Fatigue:
                    Debug.Write(LogType.Info, $"{value.Key} value {resultScore} will be applied to Fatigue Level");
                    var fatigueChange = (CurrentState == DirectorState.Relax) ? -deltaTime * 2f : deltaTime * 0.5f;
                    fatigueChange = resultScore > 0 ? resultScore * fatigueChange : resultScore / fatigueChange;
                    FatigueLevel = Mathf.Clamp((FatigueLevel + fatigueChange), 0f, 100f);
                break;
            }
            EngagementLevel = (CurrentIntensity, StressLevel) switch
            {
                ( > 70f, _) and (_, < 80f) => Mathf.Lerp(EngagementLevel, 100f, deltaTime * 2f),
                ( < 20f, _) or (_, > 90f) => Mathf.Lerp(EngagementLevel, 30f, deltaTime),
                _ => Mathf.Lerp(EngagementLevel, 60f, deltaTime)
            };
        }
    }

    /// <summary>
    /// Updates the pacing state based on the current intensity, fatigue, stress levels, and elapsed time.
    /// </summary>
    /// <remarks> This method should be called regularly, such as once per frame or update cycle, to ensure the pacing state transitions appropriately. 
    /// State transitions may trigger side effects such as invoking state changem events.</remarks>
    /// <param name="deltaTime">The time, in seconds, since the last update. Used to advance the pacing state logic.</param>
    internal void UpdatePacingState(float deltaTime)
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
            OnStateChanged(CurrentState, newState);
            timeInCurrentState = 0f;
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
        var decayRate = CurrentState switch
        {
            DirectorState.Peak => IntensityDecayRate * 0.3f,
            DirectorState.Fade => IntensityDecayRate * 2f,
            DirectorState.Relax => IntensityDecayRate * 1.5f,
            _ => IntensityDecayRate
        };

        CurrentIntensity = Mathf.Max(0f, (CurrentIntensity - decayRate * deltaTime));
    }

    /// <summary>
    /// Handles changes in the pacing state by responding to transitions between states.
    /// </summary>
    /// <param name="oldState">The previous pacing state before the transition occurred.</param>
    /// <param name="newState">The new pacing state after the transition.</param>
    void OnStateChanged(DirectorState oldState, DirectorState newState)
    {
        Debug.Log($"[Director] State: {oldState} -> {newState} (Intensity: {CurrentIntensity:F1}, Stress: {StressLevel:F1})");
        oldState = newState;
    }

    /// <summary>
    /// Records the current intensity event and adds it to the intensity history if sufficient time has elapsed since the last event.
    /// </summary>
    /// <remarks>This method enforces a minimum interval of one second between recorded intensity events. 
    /// The intensity history is capped at 50 events; when this limit is exceeded, the oldest event is removed. 
    /// This helps maintain a recent history of intensity changes for further analysis or processing.</remarks>
    internal void RecordIntensityEvent()
    {
        if (intensityHistory.Count > 0)
        {
            var lastEvent = intensityHistory.ToArray().Last();
            if (Time.GameTime - lastEvent.Time < 1f) return;
        }

        intensityHistory.Enqueue(new IntensityEvent
        {
            Time = Time.GameTime,
            Intensity = CurrentIntensity,
            Delta = 0f,
            Reason = $"State: {CurrentState}"
        });

        if (intensityHistory.Count > 50) intensityHistory.Dequeue();
    }

    /// <summary>
    /// Gets the current difficulty multiplier based on the pacing state and intensity.
    /// </summary>
    /// <remarks>The multiplier reflects the current challenge level, adjusting dynamically according to the
    /// pacing state and relevant parameters such as intensity and elapsed time. Typical values range from 0.5 during
    /// relaxed states up to 1.3 at peak intensity.</remarks>
    public float DifficultyMultiplier => CurrentState switch
    {
        DirectorState.Build => Mathf.Lerp(0.8f, 1.0f, CurrentIntensity / PeakThreshold),
        DirectorState.Peak => Mathf.Lerp(1.0f, 1.3f, timeInCurrentState / MaxPeakDuration),
        DirectorState.Fade => Mathf.Lerp(1.0f, 0.7f, timeInCurrentState / 10f),
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
        var recentEvents = new List<IntensityEvent>();

        intensityHistory.ForEach( evt =>
        {
            if (evt.Time >= cutOffTime) recentEvents.Add(evt);
        });

        if (recentEvents.Count == 0) return CurrentIntensity;

        float sum = 0;
        recentEvents.ForEach(evt => sum += evt.Intensity);

        return sum / recentEvents.Count;
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
