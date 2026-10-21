using FlaxEngine;
using OIDDA.Data;
using System.Collections.Generic;

namespace OIDDA;

/// <summary>
/// Metrics Aggregator
/// </summary>
public static class MetricsAggregator
{
    public static float CalculateOverallScore(List<Metrics> metrics, Dictionary<string, object> currentValues)
    {
        if (metrics == null || metrics.Count == 0) return 0.5f;

        var totalWeightedScore = 0f;
        var totalWeight = 0f;

        foreach (var metric in metrics)
        {
            if (currentValues.TryGetValue(metric.MetricName, out object value))
            {
                totalWeightedScore += metric.CalculateWeightedScore(value);
                totalWeight += metric.Weight;
            }
        }

        if (totalWeight > 0) return Mathf.Saturate(totalWeightedScore / totalWeight);

        return 0.5f;
    }

    public static MetricsAnalysis Analyze(List<Metrics> metrics, Dictionary<string, object> currentValues,
        float easyThreshold = OIDDADefaults.EasyThreshold, float difficultThreshold = OIDDADefaults.DifficultThreshold)
    {
        var analysis = new MetricsAnalysis
        {
            OverallScore = CalculateOverallScore(metrics, currentValues),
            MetricInfos = new List<MetricInfo>()
        };

        foreach (var metric in metrics)
        {
            if (!currentValues.TryGetValue(metric.MetricName, out var value)) continue;
            analysis.MetricInfos.Add(metric.GetInfo(value));
        }

        return analysis with
        {
            OverallState = DetermineOverallState(analysis.OverallScore, easyThreshold, difficultThreshold)
        };
    }

    public static List<MetricInfo> GetProblematicMetrics(List<Metrics> metrics, Dictionary<string, object> currentValues, float threshold = OIDDADefaults.DifficultThreshold)
    {
        var problematic = new List<MetricInfo>();

        foreach (var metric in metrics)
        {
            if (!currentValues.TryGetValue(metric.MetricName, out var currentValue)) continue;

            if (metric.IndicatesDifficulty(currentValue, threshold))
            {
                problematic.Add(metric.GetInfo(currentValue));
            }
        }

        problematic.Sort((a, b) => b.NormalizedScore.CompareTo(a.NormalizedScore));
        
        return problematic;
    }

    private static DifficultyState DetermineOverallState(float score, float easyThreshold, float difficultThreshold) => score switch
    {
        var s when s > difficultThreshold => DifficultyState.TooDifficult,
        var s when s < easyThreshold => DifficultyState.TooEasy,
        _ => DifficultyState.Balanced
    };
}

public struct MetricsAnalysis
{
    public float OverallScore;              
    public DifficultyState OverallState;
    public List<MetricInfo> MetricInfos;
}