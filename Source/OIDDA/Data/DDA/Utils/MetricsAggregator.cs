using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// Metrics Aggregator
/// </summary>
public static class MetricsAggregator
{
    public static float CalculateOverallScore(List<OIDDAMetrics> metrics, Dictionary<string, object> currentValues)
    {
        if (metrics == null || metrics.Count == 0) return 1f;

        var totalWeightedScore = 0f;
        var totalWeight = 0f;

        foreach(var metric in metrics)
        {
            if (!currentValues.ContainsKey(metric.MetricName)) continue;

            var CurrentValue = currentValues[metric.MetricName];

            totalWeightedScore += metric.CalculateWeightedScore(CurrentValue);
            totalWeight += metric.Weight;
        }

        if (totalWeight > 0) return Mathf.Clamp(totalWeightedScore / totalWeight, 0f, 2f);

        return 1f;
    }

    public static MetricsAnalysis Analyze(List<OIDDAMetrics> metrics, Dictionary<string, object> currentValues)
    {
        var analysis = new MetricsAnalysis
        {
            OverallScore = CalculateOverallScore(metrics, currentValues),
            MetricInfos = new List<MetricInfo>()
        };

        foreach(var metric in metrics)
        {
            if (!currentValues.ContainsKey(metric.MetricName)) continue;
            var info = metric.GetInfo(currentValues[metric.MetricName]);
            analysis.MetricInfos.Add(info);
        }

        return analysis with
        {
            OverallState = DetermineOverallState(analysis.OverallScore)
        };
    }

    public static List<MetricInfo> GetProblematicMetrics(List<OIDDAMetrics> metrics, Dictionary<string, object> currentValues, float threshold = 1.7f)
    {
        var problematic = new List<MetricInfo>();

        foreach (var metric in metrics)
        {
            if (!currentValues.ContainsKey(metric.MetricName)) continue;

            var currentValue = currentValues[metric.MetricName];
            if (metric.IndicatesDifficulty(currentValue, threshold))
            {
                problematic.Add(metric.GetInfo(currentValue));
            }
        }

        problematic.Sort((a, b) => b.NormalizedScore.CompareTo(a.NormalizedScore));
        
        return problematic;
    }

    static DifficultyState DetermineOverallState(float score) => score switch
    {
        > 1.7f => DifficultyState.TooDifficult,
        < 0.3f => DifficultyState.TooEasy,
        _ => DifficultyState.Balanced
    };
}

public struct MetricsAnalysis
{
    public float OverallScore;              
    public DifficultyState OverallState;
    public List<MetricInfo> MetricInfos;
}

public enum DifficultyState
{
    TooEasy,
    Balanced,
    TooDifficult
}