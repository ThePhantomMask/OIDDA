using FlaxEngine;
using System;

namespace OIDDA;

/// <summary>
/// DirectorMetrics class.
/// </summary>
public class DirectorMetrics
{
    public string MetricName;
    [Range(0, 1)] public float Weight = 0.5f;
    public bool InverseLogic;

    public float CalculateScore(object currentValue, float ThresholdMin, float ThresholdMax)
    {
        try
        {
            return ConvertToFloat(currentValue);
        }
        catch(NullReferenceException e)
        {
            Debug.LogException(e);
            return 1;
        }
    }

    public float CalculateWeightedScore(object currentValue, float ThresholdMin, float ThresholdMax) => CalculateScore(currentValue, ThresholdMin, ThresholdMax) * Weight;

    protected float ConvertToFloat(object value) =>
        value switch
        {
            float f => f,
            int i => (float)i,
            bool b => b ? 1f : 0f,
            Vector2 v2 => v2.Length,
            Vector3 v3 => v3.Length,
            Vector4 v4 => v4.Length,
            Quaternion q => q.Length,
            Color c => c.ValuesSum,
            Transform t => t.Translation.Length,
            Matrix m => m.TranslationVector.Length,
            _ => 0f
        };
}
