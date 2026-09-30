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
    public Rule()
    {
    }

    private bool isNotException => this is not RuleException;

    [VisibleIf(nameof(isNotException))] public string RuleName;
    public List<RuleValue> Values = new();
    public RuleApplicationContext Context = RuleApplicationContext.Always;
    public DDACondition Condition;
    [VisibleIf(nameof(isNotException))] public virtual List<RuleException> RuleExceptions { get; set; }

    protected virtual bool FilterValuesByScore => true;

    public virtual bool IsContextSatisfied(in DifficultyContext ctx) => ctx.Matches(Context);

    public IEnumerable<RuleValue> GetValuesToApply(Dictionary<string, object> metrics, DifficultyContext ctx)
    {
        if (TryGetActiveException(metrics, ctx, out var exception))
            return exception.GetOwnValues(ctx);

        if (Condition != null && !Condition.IsMet(metrics)) return Array.Empty<RuleValue>();
        if (!IsContextSatisfied(ctx)) return Array.Empty<RuleValue>();

        return GetOwnValues(ctx);
    }

    protected IEnumerable<RuleValue> GetOwnValues(DifficultyContext ctx)
    {
        if (Values == null) yield break;

        foreach (var v in Values)
        {
            if (v == null) continue;
            if (FilterValuesByScore && !v.MatchesScore(ctx)) continue;
            yield return v;
        }
    }

    /// <summary>
    /// Applies the current rule to the specified metrics if the associated condition is satisfied.
    /// </summary>
    /// <param name="metrics">A dictionary representing metrics that influence the evaluation and application of the rule, cannot be null.</param>
    ///  /// <param name="ctx">.</param>
    public virtual void Apply(Dictionary<string, object> metrics, DifficultyContext ctx)
    {
        if(isNotException) Debug.Write(LogType.Info, $"Applying rule {RuleName}");
        foreach (var v in GetValuesToApply(metrics, ctx))
            v.ApplyToGlobals();
    }

    /// <summary>
    /// Determines whether there is an active exception that satisfies the specified conditions based on the provided metrics.
    /// </summary>
    /// <param name="metrics">A dictionary containing key-value pairs representing metrics used to evaluate exception conditions, cannot be null.</param>
    /// <param name="ctx"></param>
    /// <param name="activeException">When the exception is true, this output parameter contains the active exception that met the condition./>.</param>
    protected bool TryGetActiveException(Dictionary<string, object> metrics, in DifficultyContext ctx, out RuleException activeException)
    {
        activeException = null;
        
        if (RuleExceptions == null) return false;

        foreach (var e in RuleExceptions)
        {
            if (e?.Condition != null && e.Condition.IsMet(metrics) && e.IsContextSatisfied(ctx))
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


    public bool MatchesScore(in DifficultyContext ctx) =>
        ctx.Score > ctx.DifficultThreshold ? Operator == AdjustmentOperator.Subtract || Operator == AdjustmentOperator.Set :
        ctx.Score < ctx.EasyThreshold ? Operator == AdjustmentOperator.Add || Operator == AdjustmentOperator.Multiply :
        false;

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

public readonly struct DifficultyContext
{
    public readonly float Score, EasyThreshold, DifficultThreshold;

    public DifficultyContext(float score, float easyThreshold, float difficultThreshold)
    {
        Score = score;
        EasyThreshold = easyThreshold;
        DifficultThreshold = difficultThreshold;
    }

    public bool Matches(RuleApplicationContext context) => context switch
    {
        RuleApplicationContext.Always => true,
        RuleApplicationContext.WhenTooDifficult => Score > DifficultThreshold,
        RuleApplicationContext.WhenTooEasy => Score < EasyThreshold,
        RuleApplicationContext.WhenBalanced => Score >= EasyThreshold && Score <= DifficultThreshold,
        _ => false,
    };
}