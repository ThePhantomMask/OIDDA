using System;
using System.Collections.Generic;
using OIDDA.Data;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// DirectorRule class.
/// </summary>
public class DirectorRule
{
    bool isNotException => this is not DirectorRuleException;

    [VisibleIf(nameof(isNotException))] public string RuleName;
    public string TargetGlobal;
    public GameplayValue Value;
    public GameplayValue MinValue;
    public GameplayValue MaxValue;
    public RuleApplicationContext Context = RuleApplicationContext.Always;
    public EmotionType Emotion = EmotionType.Stress;
    public DirectorCondition Condition;
    public AdjustmentOperator Operator;
    [VisibleIf(nameof(isNotException))] public List<DirectorRuleException> RuleExceptions;


    public virtual void Apply(Dictionary<string, object> metrics)
    {
        if (!Condition.IsMet(metrics)) return;

        if (IsHasActiveException(metrics, out var exception))
        {
            exception.Apply(metrics);
            return;
        }

        if (isNotException) Debug.Write(LogType.Info, $"Applying Director rule {RuleName}");
        ApplyToGlobalsVariables();
    }


    protected bool IsHasActiveException(Dictionary<string, object> metrics, out DirectorRuleException activeException)
    {
        activeException = null;
        if (RuleExceptions is null || RuleExceptions.Count is 0) return false;

        foreach (var exception in RuleExceptions)
        {
            if (exception.Condition.IsMet(metrics))
            {
                activeException = exception;
                return true;
            }
        }
        return false;
    }

    protected virtual void ApplyToGlobalsVariables()
    {
        var currentValue = GameplayValue.ConvertObject(ORS.Instance.QuickReceiver<object>(TargetGlobal));
        var newValue = GameplayValueOperations.Apply(currentValue, new GameplayValue(), Operator);
        newValue = GameplayValueOperations.Clamp(newValue, MinValue, MaxValue);
        ORS.Instance.QuickSender(TargetGlobal, newValue.Value);
    }
}
