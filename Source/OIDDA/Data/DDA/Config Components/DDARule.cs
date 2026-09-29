using FlaxEngine;
using OIDDA.Data;
using System;
using System.Collections.Generic;

namespace OIDDA;

/// <summary>
/// OIDDA Rule
/// </summary>
[Category(name: "OIDDA Data")]
public class Rule
{
    bool isNotException => this is not RuleException;

    [VisibleIf(nameof(isNotException))] public string RuleName;
    public List<RuleValue> Values = new();
    public RuleApplicationContext Context = RuleApplicationContext.Always;
    public DDACondition Condition;
    [VisibleIf(nameof(isNotException))] public virtual List<RuleException> RuleExceptions { get; set; }


    /// <summary>
    /// Applies the current rule to the specified metrics if the associated condition is satisfied.
    /// </summary>
    /// <param name="metrics">A dictionary representing metrics that influence the evaluation and application of the rule, cannot be null.</param>
    public virtual void Apply(Dictionary<string, object> metrics)
    {
        if (Condition != null && !Condition.IsMet(metrics)) return;

        if (TryGetActiveException(metrics, out var exception))
        {
            exception.Apply(metrics);
            return;
        }

        if(isNotException) Debug.Write(LogType.Info, $"Applying rule {RuleName}");
        ApplyValues();
    }

    /// <summary>
    /// Determines whether there is an active exception that satisfies the specified conditions based on the provided metrics.
    /// </summary>
    /// <param name="metrics">A dictionary containing key-value pairs representing metrics used to evaluate exception conditions, cannot be null.</param>
    /// <param name="activeException">When the exception is true, this output parameter contains the active exception that met the condition./>.</param>
    protected bool TryGetActiveException(Dictionary<string, object> metrics, out RuleException activeException)
    {
        activeException = null;
        
        if (RuleExceptions == null) return false;

        foreach (var e in RuleExceptions)
        {
            if (e?.Condition != null && e.Condition.IsMet(metrics))
            {
                activeException = e;
                return true;
            }
        }
        return false;
    }

    protected virtual void ApplyValues()
    {
        if (Values == null) return;
        foreach (var v in Values)
            v?.ApplyToGlobals();
    }
}

/// <summary>
/// OIDDA Rule Values
/// </summary>
public class RuleValue
{
    public string TargetGlobal;
    public GameplayValue Value;
    public GameplayValue MinValue;
    public GameplayValue MaxValue;
    public AdjustmentOperator Operator;

    /// <summary>
    /// Applies the specified gameplay value operation to the global variables.
    /// </summary>
    public void ApplyToGlobals()
    {
        var currentValue = GameplayValue.ConvertObject(ORS.Instance.QuickReceiver<object>(TargetGlobal));
        var newValue = GameplayValueOperations.Apply(currentValue, new GameplayValue(), Operator);
        newValue = GameplayValueOperations.Clamp(newValue, MinValue, MaxValue);
        ORS.Instance.QuickSender(TargetGlobal, newValue.Value);
    }
}