using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// OIDDA Metrics
/// </summary>
public class OIDDAMetrics
{
    public string MetricName;
    [Range(0, 2)] public float Weight = 1;
    public float ThresholdMin;
    public float ThresholdMax;
    public bool InverseLogic;

    public float CalculateScore(object currentValue)
    {
        try
        {
            var floatValue = ConvertToFloat(currentValue);
            return Normalize(floatValue);
        }
        catch(NullReferenceException e)
        {
            Debug.LogException(e);
            return 1;
        }
    }

    protected float Normalize(float value)
    {
        float range = ThresholdMax - ThresholdMin;

        if (range <= 0) return value > ThresholdMin ? 2f : 0f;

        float normalized = (value - ThresholdMin) / range;
        normalized = Mathf.Clamp(normalized, 0f, 2f);

        if (InverseLogic) normalized = 2f - normalized;

        return normalized;
    }

    public float CalculateWeightedScore(object currentValue) => CalculateScore(currentValue) * Weight;

    public bool IsOutOfBounds(object currentValue)
    {
        if (currentValue == null) return false;

        float floatValue = ConvertToFloat(currentValue);

        return InverseLogic ? floatValue < ThresholdMin : floatValue > ThresholdMax;
    }

    public bool IndicatesDifficulty(object currentValue, float threshold = 1.7f) => CalculateScore(currentValue) > threshold;

    public bool IndicatesEasy(object currentValue, float threshold = 0.3f) => CalculateScore(currentValue) < threshold;

    public MetricInfo GetInfo(object currentValue)
    {
        float score = CalculateScore(currentValue);

        return new MetricInfo
        {
            MetricName = MetricName,
            CurrentValue = currentValue,
            NormalizedScore = score,
            WeightedScore = score * Weight,
            Weight = Weight,
            IsOutOfBounds = IsOutOfBounds(currentValue),
            State = DetermineState(score)
        };
    }

    protected float ConvertToFloat(object value)
    {
        return value switch
        {
            float f => f,
            int i => (float)i,
            double d => (float)d,
            bool b => b ? 2f : 0f,
            Vector3 v3 => v3.Length,
            Vector2 v2 => v2.Length,
            _ => 0f
        };
    }

    MetricState DetermineState(float score) => score switch
    {
        > 1.7f => MetricState.Critical,
        > 1.5f => MetricState.Warning,
        < 1.5f and > 0.3f => MetricState.Normal,
        _ => MetricState.Good
    };
}

public struct MetricInfo
{
    public string MetricName;
    public object CurrentValue;
    public float NormalizedScore;    // 0-1
    public float WeightedScore;      // Score * Weight
    public float Weight;
    public bool IsOutOfBounds;
    public MetricState State;
}

public enum MetricState
{
    Good,
    Normal,
    Warning,
    Critical
}
