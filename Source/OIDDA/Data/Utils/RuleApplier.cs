using FlaxEngine;
using OIDDA.Data;
using System;
using System.Collections.Generic;

namespace OIDDA;

/// <summary>
/// Applies rule values to the gameplay globals, directly or through a <see cref="SmoothingManager"/>.
/// Shared by the DDA manager and the Director so the logic lives in one place.
/// </summary>
internal static class RuleApplier
{
    /// <summary>
    /// Evaluates a rule and applies every value it produces.
    /// </summary>
    /// <returns><see langword="true"/> if the rule produced at least one value.</returns>
    public static bool ApplyRule<TCondition, TException>(
        RuleBase<TCondition, TException> rule,
        Dictionary<string, object> currentValues,
        in DifficultyContext ctx,
        bool useSmoothing,
        SmoothingManager smoothing,
        float smoothingSpeed,
        bool debug)
        where TCondition : ConditionBase
        where TException : RuleBase<TCondition, TException>
    {
        var anyApplied = false;

        foreach (var rv in rule.GetValuesToApply(currentValues, ctx))
        {
            anyApplied = true;

            if (useSmoothing) ApplySmooth(rv, currentValues, smoothing, smoothingSpeed, debug);
            else rv.ApplyToGlobals();
        }

        return anyApplied;
    }

    static void ApplySmooth(RuleValue rv, Dictionary<string, object> currentValues, SmoothingManager smoothing, float smoothingSpeed, bool debug)
    {
        if (string.IsNullOrEmpty(rv.TargetGlobal)) return;

        try
        {
            var targetValue = GameplayValue.ConvertObject(currentValues[rv.TargetGlobal]);
            var newValue = GameplayValueOperations.Apply(targetValue, rv.Value, rv.Operator);
            newValue = GameplayValueOperations.Clamp(newValue, rv.MinValue, rv.MaxValue);
            smoothing.SetTarget(rv.TargetGlobal, newValue, smoothingSpeed);

            if (debug)
                Debug.Log($"[OIDDA] Smoothing: {rv.TargetGlobal} {targetValue.Value} -> {newValue.Value} (speed: {smoothingSpeed})");
        }
        catch (Exception e)
        {
            Debug.LogError($"[OIDDA] Error in smooth apply ({rv.TargetGlobal}): {e.Message}");
        }
    }
}
