using FlaxEngine;
using OIDDA.Data;
using System.Collections.Generic;

namespace OIDDA;

/// <summary>
/// Combines one value per player into one value for the group.
/// </summary>
internal static class GroupAggregator
{
    /// <param name="values">One value per player.</param>
    /// <param name="weights">One weight per player (same order); only used by <see cref="GroupAggregation.Weighted"/>.</param>
    /// <param name="fallback">Returned when there are no values.</param>
    /// <param name="scratch">Reusable list, to avoid allocating when sorting.</param>
    public static float Aggregate(List<float> values, List<float> weights, GroupAggregation mode, float percentile, float fallback, List<float> scratch)
    {
        int count = values.Count;
        if (count == 0) return fallback;

        switch (mode)
        {
            case GroupAggregation.Max:
            {
                float max = values[0];
                for (int i = 1; i < count; i++) if (values[i] > max) max = values[i];
                return max;
            }
            case GroupAggregation.Weighted:
            {
                float sum = 0f, weightSum = 0f;
                for (int i = 0; i < count; i++)
                {
                    float w = i < weights.Count ? weights[i] : 1f;
                    sum += values[i] * w;
                    weightSum += w;
                }
                return weightSum > 0f ? sum / weightSum : Average(values);
            }
            case GroupAggregation.Percentile:
            {
                scratch.Clear();
                scratch.AddRange(values);
                scratch.Sort();

                float position = Mathf.Clamp(percentile, 0f, 1f) * (count - 1);
                int low = (int)System.Math.Floor(position);
                int high = System.Math.Min(low + 1, count - 1);
                return Mathf.Lerp(scratch[low], scratch[high], position - low);
            }
            default:
                return Average(values);
        }
    }

    static float Average(List<float> values)
    {
        float sum = 0f;
        for (int i = 0; i < values.Count; i++) sum += values[i];
        return sum / values.Count;
    }
}
