using FlaxEngine;
using OIDDA.OIDDA.Data;
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
    public string TargetGlobal;
    public GameplayValue Value;
    public GameplayValue MinValue;
    public GameplayValue MaxValue;
    public RuleApplicationContext Context = RuleApplicationContext.Always;
    public OIDDACondition Condition;
    public AdjustmentOperator Operator;
    [VisibleIf(nameof(isNotException))] public List<RuleException> RuleExceptions;


    /// <summary>
    /// Applies the current rule to the specified metrics if the associated condition is satisfied.
    /// </summary>
    /// <param name="metrics">A dictionary representing metrics that influence the evaluation and application of the rule, cannot be null.</param>
    public virtual void Apply(Dictionary<string, object> metrics)
    {
        if (!Condition.IsMet(metrics)) return;

        if (IsHasActiveException(metrics, out var exception))
        {
            exception.Apply(metrics);
            return;
        }

        if(isNotException) Debug.Write(LogType.Info, $"Applying rule {RuleName}");
        ApplyToGlobalsVariables();
    }

    /// <summary>
    /// Determines whether there is an active exception that satisfies the specified conditions based on the provided metrics.
    /// </summary>
    /// <param name="metrics">A dictionary containing key-value pairs representing metrics used to evaluate exception conditions, cannot be null.</param>
    /// <param name="activeException">When the exception is true, this output parameter contains the active exception that met the condition./>.</param>
    protected bool IsHasActiveException(Dictionary<string, object> metrics, out RuleException activeException)
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

    /// <summary>
    /// Applies the specified gameplay value operation to the global variables.
    /// </summary>
    protected virtual void ApplyToGlobalsVariables()
    {
        var currentValue = GameplayValue.ConvertObject(ORS.Instance.QuickReceiver<object>(TargetGlobal));
        var newValue = GameplayValueOperations.Apply(currentValue, new GameplayValue(), Operator);
        newValue = GameplayValueOperations.Clamp(newValue, MinValue, MaxValue);
        ORS.Instance.QuickSender(TargetGlobal, newValue.Value);
    }
}