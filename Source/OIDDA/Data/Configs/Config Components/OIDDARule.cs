using FlaxEngine;
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

/// <summary>
/// Specifies the types of adjustment operations that can be applied to a value, such as addition, subtraction, multiplication, division, assignment, toggling a boolean, or appending a string.
/// </summary>
public enum AdjustmentOperator
{
    /// <summary>
    /// Adds two values.
    /// </summary>
    Add,
    /// <summary>
    /// Subtracts one number from another.
    /// </summary>
    Subtract,
    /// <summary>
    /// Calculates the product of two values.
    /// </summary>
    Multiply,
    /// <summary>
    /// Divides two values.
    /// </summary>
    Divide,
    /// <summary>
    /// Sets the new value
    /// </summary>
    Set,
    /// <summary>
    /// Only for the Bool type value
    /// </summary>
    Toggle,
    /// <summary>
    /// Only for the String type value
    /// </summary>
    Append
}

/// <summary>
/// Specifies the contexts in which a rule can be applied during evaluation.
/// </summary>
public enum RuleApplicationContext
{
    /// <summary>
    /// When the rule is always applied
    /// </summary>
    Always,
    /// <summary>
    /// When the rule is applied when the game is hard
    /// </summary>
    WhenTooDifficult,
    /// <summary>
    /// When the rule is applied when the game is easy
    /// </summary>
    WhenTooEasy,
    /// <summary>
    /// When the rule is applied when the game is balanced
    /// </summary>
    WhenBalanced,
    /// <summary>
    /// When the rule is applied when didn't none context
    /// </summary>
    None
}