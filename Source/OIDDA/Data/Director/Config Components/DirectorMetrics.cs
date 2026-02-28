using FlaxEngine;
using OIDDA.Data;
using System;

namespace OIDDA;

/// <summary>
/// Director Metrics
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
            return Normalize(ConvertToFloat(currentValue), ThresholdMin, ThresholdMax);
        }
        catch(NullReferenceException e)
        {
            Debug.LogException(e);
            return 1;
        }
    }

    protected float Normalize(float value, float ThresholdMin, float ThresholdMax) =>
         ((ThresholdMax - ThresholdMin) <= 0) ? value > ThresholdMin ? 1f : 0f :
        InverseLogic ? 1f - Mathf.Saturate((value - ThresholdMin) / (ThresholdMax - ThresholdMin)) :
        Mathf.Saturate((value - ThresholdMin) / (ThresholdMax - ThresholdMin));

    public float CalculateWeightedScore(object currentValue, float ThresholdMin, float ThresholdMax) => CalculateScore(currentValue, ThresholdMin, ThresholdMax) * Weight;

    public bool IsOutOfBounds(object currentValue, float ThresholdMin, float ThresholdMax) => currentValue is null ? false : InverseLogic ? ConvertToFloat(currentValue) < ThresholdMin : ConvertToFloat(currentValue) > ThresholdMax;

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

    MetricState DetermineState(float score) => score switch
    {
        > 0.7f => MetricState.Critical,
        > 0.5f => MetricState.Warning,
        < 0.5f and > 0.3f => MetricState.Normal,
        _ => MetricState.Good
    };
}
