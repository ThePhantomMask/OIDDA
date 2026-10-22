using FlaxEngine;
using System;
using System.Collections.Generic;

namespace OIDDA.Data;

public abstract class ConditionBase
{
    public List<ConditionClause> Clauses;
    public bool RequireAll = true;  // true = AND, false = OR

    public bool IsMet(Dictionary<string, object> metrics)
    {
        if (Clauses == null || Clauses.Count == 0) return true;
        
        if (RequireAll)
        {
            foreach (var clause in Clauses)
            {
                if (clause != null && !clause.Evaluate(metrics))
                    return false;
            }

            return true;
        }

        foreach (var clause in Clauses)
        {
            if (clause != null && clause.Evaluate(metrics))
                return true;
        }

        return false;
    }
}

[Serializable]
public class ConditionClause
{
    public string MetricName;
    public ComparisonOperator Operator;
    public GameplayValue CompareValue;

    public bool Evaluate(Dictionary<string, object> metrics)
    {
        if (!metrics.TryGetValue(MetricName, out var rawValue)) return false;

        var metricValue = GameplayValue.ConvertObject(rawValue);
        return GameplayValueOperations.Compare(metricValue, CompareValue, Operator);
    }
}

public abstract class MetricBase
{
    protected const float NoDataScore = 1f;

    public string MetricName;
    [Range(0, 1)] public float Weight = 0.5f;
    public float ThresholdMin;
    public float ThresholdMax;
    public bool InverseLogic;

    public float CalculateScore(object currentValue) =>
        currentValue is null ? NoDataScore : Normalize(ConvertToFloat(currentValue));

    public float CalculateWeightedScore(object currentValue) => CalculateScore(currentValue) * Weight;

    public bool IsOutOfBounds(object currentValue)
    {
        if (currentValue == null)
            return false;

        float value = ConvertToFloat(currentValue);
        return InverseLogic ? value < ThresholdMin : value > ThresholdMax;
    }

    public MetricInfo GetInfo(object currentValue)
    {
        float score = CalculateScore(currentValue);

        return new MetricInfo
        {
            MetricName = MetricName,
            CurrentValue = currentValue,
            NormalizedScore = score,
            WeightedScore = score * Weight,
            Weight = Weight,
            IsOutOfBounds = IsOutOfBounds(currentValue),
            State = DetermineState(score)
        };
    }

    protected float Normalize(float value)
    {
        var range = ThresholdMax - ThresholdMin;

        if (range <= 0f)
        {
            float step = value > ThresholdMin ? 1f : 0f;
            return InverseLogic ? 1f - step : step;
        }

        float t = Mathf.Saturate((value - ThresholdMin) / range);
        return InverseLogic ? 1f - t : t;
    }

    protected float ConvertToFloat(object value) =>
        value switch
        {
            float f => f,
            int i => (float)i,
            bool b => b ? 1f : 0f,
            Vector2 v2 => v2.Length,
            Vector3 v3 => v3.Length,
            Vector4 v4 => v4.Length,
            Quaternion q => q.Length,
            Color c => c.ValuesSum,
            Transform t => t.Translation.Length,
            Matrix m => m.TranslationVector.Length,
            _ => 0f
        };

    protected static MetricState DetermineState(float score) => score switch
    {
        > OIDDADefaults.DifficultThreshold => MetricState.Critical,
        > OIDDADefaults.WarningThreshold => MetricState.Warning,
        > OIDDADefaults.EasyThreshold => MetricState.Normal,
        _ => MetricState.Good
    };
}

public abstract class RuleBase<TCondition, TException>
    where TCondition : ConditionBase
    where TException : RuleBase<TCondition, TException>
{
    protected bool isNotException => this is not TException;
    protected virtual bool showEmotion => false;

    [VisibleIf(nameof(isNotException))] public string RuleName;
    public List<RuleValue> Values = new();
    public RuleApplicationContext Context = RuleApplicationContext.Always;
    public TCondition Condition;
    [VisibleIf(nameof(showEmotion))] public EmotionType Emotion = EmotionType.Stress;
    [VisibleIf(nameof(isNotException))] public virtual List<TException> RuleExceptions { get; set; }

    protected virtual bool FilterValuesByScore => true;

    /// <summary>
    /// By default a rule value only fires for the operators that fit the current score (Add/Multiply when too easy,
    /// Subtract/Set when too difficult). Enable this to skip that filter and let <see cref="Context"/> alone decide,
    /// which also makes Divide, Toggle, Append and the WhenBalanced context usable.
    /// </summary>
    [VisibleIf(nameof(isNotException))] public bool IgnoreScoreFilter = false;

    protected bool IsConditionMet(TCondition condition, Dictionary<string, object> metrics) => condition.IsMet(metrics);

    public virtual bool IsContextSatisfied(in DifficultyContext ctx) => ctx.Matches(Context);

    public IEnumerable<RuleValue> GetValuesToApply(Dictionary<string, object> metrics, DifficultyContext ctx)
    {
        if (TryGetActiveException(metrics, ctx, out var exception))
            return exception.GetOwnValues(ctx);

        if (Condition != null && !IsConditionMet(Condition, metrics)) return Array.Empty<RuleValue>();
        if (!IsContextSatisfied(ctx)) return Array.Empty<RuleValue>();

        return GetOwnValues(ctx);
    }

    protected internal IEnumerable<RuleValue> GetOwnValues(DifficultyContext ctx)
    {
        if (Values == null) yield break;

        foreach (var v in Values)
        {
            if (v == null) continue;
            if (FilterValuesByScore && !IgnoreScoreFilter && !v.MatchesScore(ctx)) continue;
            yield return v;
        }
    }
    /// <summary>
    /// Determines whether there is an active exception that satisfies the specified conditions based on the provided metrics.
    /// </summary>
    /// <param name="metrics">A dictionary containing key-value pairs representing metrics used to evaluate exception conditions, cannot be null.</param>
    /// <param name="ctx"></param>
    /// <param name="activeException">When the exception is true, this output parameter contains the active exception that met the condition./>.</param>
    protected bool TryGetActiveException(Dictionary<string, object> metrics, in DifficultyContext ctx, out TException activeException)
    {
        activeException = null;

        if (RuleExceptions == null) return false;

        foreach (var e in RuleExceptions)
        {
            if (e?.Condition != null && IsConditionMet(e.Condition, metrics) && e.IsContextSatisfied(ctx))
            {
                activeException = e;
                return true;
            }
        }

        return false;
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
    /// True when Min/Max were set. Both left at their default (Float 0, 0) means "no clamp": otherwise a Float result would always be forced to 0.
    /// </summary>
    public bool HasClamp => !(MinValue.Type == ValueType.Float && MaxValue.Type == ValueType.Float &&
                              MinValue.FloatValue == 0f && MaxValue.FloatValue == 0f);

    /// <summary>
    /// Clamps a result to Min/Max, unless no limits were set.
    /// </summary>
    public GameplayValue ClampResult(GameplayValue value) => HasClamp ? GameplayValueOperations.Clamp(value, MinValue, MaxValue) : value;

    /// <summary>
    /// Applies the specified gameplay value operation to the global variables.
    /// </summary>
    public void ApplyToGlobals() => ApplyToGlobals(null);

    /// <summary>
    /// Applies the operation to the given store. With a null store the default ORS (single-player behaviour) is used.
    /// </summary>
    public void ApplyToGlobals(IValueStore store)
    {
        var current = store != null ? store.GetValue<object>(TargetGlobal) : ORS.Instance.QuickReceiver<object>(TargetGlobal);
        var newValue = GameplayValueOperations.Apply(GameplayValue.ConvertObject(current), Value, Operator);
        newValue = ClampResult(newValue);

        // Internal write: must not trigger a new analysis while rules are being applied.
        if (store != null) store.SetValue(TargetGlobal, newValue.Value);
        else ORS.Instance.WriteValue(TargetGlobal, newValue.Value);
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