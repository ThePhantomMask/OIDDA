using System;
using System.Collections.Generic;
using FlaxEngine;
using FlaxEngine.Utilities;

namespace OIDDA;

/// <summary>
/// OIDDA Rule
/// </summary>
[Category(name: "OIDDA Data")]
public class Rule
{
    [VisibleIf(nameof(_isNotException))] public string RuleName;
    public string TargetGlobal;
    public AdjustmentOperator Operator;
    public GameplayValue AdjustmentValue;
    public GameplayValue MinValue;
    public GameplayValue MaxValue;
    public RuleApplicationContext ApplicationContext = RuleApplicationContext.Always;
    public OIDDACondition Condition;
    [VisibleIf(nameof(_isNotException))] public List<RuleException> Exceptions;

    bool _isNotException => this is not RuleException;

    public virtual void Apply(Dictionary<string, object> metrics)
    {
        if (!Condition.IsMet(metrics)) return;

        if (IsHasActiveException(metrics, out var exception))
        {
            exception.Apply(metrics);
            return;
        }

        if(_isNotException) Debug.Write(LogType.Info, $"Applying rule {RuleName}");
        ApplyToGlobalsVariables();
    }

    protected bool IsHasActiveException(Dictionary<string, object> metrics, out RuleException activeException)
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
        var currentValue = GameplayValue.ConvertObject(ORS.Instance.QuickReceiver<object>(TargetGlobal));
        var newValue = GameplayValueOperations.Apply(currentValue, new GameplayValue(), Operator);
        newValue = GameplayValueOperations.Clamp(newValue, MinValue, MaxValue);
        ORS.Instance.QuickSender(TargetGlobal, newValue.Value);
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