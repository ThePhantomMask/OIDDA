using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// SmoothingManager class.
/// </summary>
public class SmoothingManager
{
    Dictionary<string, SmoothValue> _smoothedValues = new();

    public void SetTarget(string variable, GameplayValue targetValue, float smoothingSpeed)
    {
        if (_smoothedValues.ContainsKey(variable))
        {
            _smoothedValues[variable].TargetValue = targetValue;
            _smoothedValues[variable].SmoothSpeed = smoothingSpeed;
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
        var toRemove = new List<string>();

        foreach(var kvp in _smoothedValues)
        {
            var smoothValue = kvp.Value;

            var currentValue = GameplayValue.ConvertObject(ORS.Instance.QuickReceiver<object>(smoothValue.Variable));
            var newValue = GameplayValueOperations.Lerp(currentValue, smoothValue.TargetValue, smoothValue.SmoothSpeed * deltaTime);
            
            ORS.Instance.QuickSender(smoothValue.Variable, newValue.Value);

            if (GameplayValueOperations.IsNearTarget(newValue, smoothValue.TargetValue))
                toRemove.Add(kvp.Key);
        }

        toRemove.ForEach(Key => _smoothedValues.Remove(Key));
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
