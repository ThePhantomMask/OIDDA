using System;
using System.Collections.Generic;
using FlaxEngine;
using FlaxEngine.Utilities;

namespace OIDDA;

/// <summary>
/// OIDDA Rule
/// </summary>
[Category(name: "OIDDA Data")]
public class OIDDARule
{
    public string TargetGlobalVariable;
    public AdjustmentOperator Operator;
    public GameplayValue AdjustmentValue;
    public GameplayValue MinValue;
    public GameplayValue MaxValue;
    public RuleApplicationContext ApplicationContext = RuleApplicationContext.Always;
    public OIDDACondition Condition;
    public List<OIDDARuleException> Exceptions;

    public virtual void Apply(Dictionary<string, object> metrics)
    {
        if (!Condition.IsMet(metrics)) return;

        if (IsHasActiveException(metrics, out var exception))
        {
            exception.Apply(metrics);
            return;
        }

        ApplyToGlobalsVariables();
    }

    protected bool IsHasActiveException(Dictionary<string, object> metrics, out OIDDARuleException activeException)
    {
        activeException = null;
        if (Exceptions == null || Exceptions.Count is 0) return false;

        foreach (var exception in Exceptions)
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
        var currentValue = GameplayValue.FromObject(ORS.Instance.QuickReceiver<object>(TargetGlobalVariable));
        var newValue = GameplayValueOperations.Apply(currentValue, new GameplayValue(), Operator);
        newValue = GameplayValueOperations.Clamp(newValue, MinValue, MaxValue);
        ORS.Instance.QuickSender(TargetGlobalVariable, newValue.GetValue());
    }
}

public enum AdjustmentOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Set,
    /// <summary>
    /// Bool only
    /// </summary>
    Toggle,
    /// <summary>
    /// String only
    /// </summary>
    Append
}

public enum RuleApplicationContext
{
    Always,
    WhenTooDifficult,
    WhenTooEasy,
    WhenBalanced,
    None
}