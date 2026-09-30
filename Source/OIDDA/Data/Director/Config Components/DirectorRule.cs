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
    public DirectorRule()
    {
    }

    private bool isNotException => this is not DirectorRuleException;

    [VisibleIf(nameof(isNotException))] public string RuleName;
    public List<RuleValue> Values = new();
    public RuleApplicationContext Context = RuleApplicationContext.Always;
    public EmotionType Emotion = EmotionType.Stress;
    public DirectorCondition Condition;
    [VisibleIf(nameof(isNotException))] public virtual List<DirectorRuleException> RuleExceptions { get; set; }

    protected virtual bool FilterValuesByScore => true;

    public bool IsContextSatisfied(in DifficultyContext ctx) => ctx.Matches(Context);

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

    public virtual void Apply(Dictionary<string, object> metrics, DifficultyContext ctx)
    {
        if (isNotException) Debug.Write(LogType.Info, $"Applying Director rule {RuleName}");
        
        foreach (var v in GetValuesToApply(metrics, ctx))
            v.ApplyToGlobals();
    }


    protected bool TryGetActiveException(Dictionary<string, object> metrics, in DifficultyContext ctx, out DirectorRuleException activeException)
    {
        activeException = null;
        if (RuleExceptions is null || RuleExceptions.Count is 0) return false;

        foreach (var exception in RuleExceptions)
        {
            if (exception?.Condition != null && exception.Condition.IsMet(metrics) && exception.IsContextSatisfied(ctx))
            {
                activeException = exception;
                return true;
            }
        }
        return false;
    }
}
