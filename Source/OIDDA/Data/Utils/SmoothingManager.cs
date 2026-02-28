using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// SmoothingManager class.
/// </summary>
public class SmoothingManager
{
    readonly Dictionary<string, SmoothValue> _smoothedValues = new();
    readonly List<string> toRemove = new();

    public void SetTarget(string variable, GameplayValue targetValue, float smoothingSpeed)
    {
        if (_smoothedValues.TryGetValue(variable, out var existing))
        {
            existing.TargetValue = targetValue;
            existing.SmoothSpeed = smoothingSpeed;
            return;
        }

        _smoothedValues[variable] = new SmoothValue
        {
            Variable = variable,
            TargetValue = targetValue,
            SmoothSpeed = smoothingSpeed
        };

    }

    public void SmoothUpdate(float deltaTime)
    {
        if (_smoothedValues == null || _smoothedValues.Count == 0) return;

        foreach(var kvp in _smoothedValues)
        {
            var smoothValue = kvp.Value;

            var currentValue = GameplayValue.ConvertObject(ORS.Instance.QuickReceiver<object>(smoothValue.Variable));
            var t = 1f - Mathf.Pow(1f - smoothValue.SmoothSpeed, deltaTime);
            var newValue = GameplayValueOperations.Lerp(currentValue, smoothValue.TargetValue, t);

            ORS.Instance.QuickSender(smoothValue.Variable, newValue.Value);

            if (GameplayValueOperations.IsNearTarget(newValue, smoothValue.TargetValue))
                toRemove.Add(kvp.Key);
        }

        toRemove.ForEach(Key => _smoothedValues.Remove(Key));
        toRemove.Clear();
    }

    public bool HasActiveSmoothings => _smoothedValues.Count > 0;

    public int ActiveSmoothingCount => _smoothedValues.Count;

    public void Clear() => _smoothedValues.Clear();
}

class SmoothValue
{
    public string Variable;
    public GameplayValue TargetValue;
    public float SmoothSpeed;
}
