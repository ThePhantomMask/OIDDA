using System;
using System.Collections.Generic;
using System.Linq;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// OIDDA Condition
/// </summary>
public class OIDDACondition
{
    public List<ConditionClause> Clauses;
    public bool RequireAll = true;  // true = AND, false = OR

    public bool IsMet(Dictionary<string, object> metrics)
    {
        if (Clauses == null || Clauses.Count == 0) return true;
        return RequireAll ? Clauses.All(c => c.Evaluate(metrics)) : Clauses.Any(c => c.Evaluate(metrics));
    }
}

[Serializable]
public class ConditionClause
{
    public string MetricName;
    public ComparisonOperator Operator;
    public float CompareValue;

    public bool Evaluate(Dictionary<string, object> metrics)
    {
        if (!metrics.ContainsKey(MetricName)) return false;

        float value = Convert.ToSingle(metrics[MetricName]);

        return Operator switch
        {
            ComparisonOperator.Greater => value > CompareValue,
            ComparisonOperator.Less => value < CompareValue,
            ComparisonOperator.GreaterOrEqual => value >= CompareValue,
            ComparisonOperator.LessOrEqual => value <= CompareValue,
            ComparisonOperator.Equal => Math.Abs(value - CompareValue) < 0.001f,
            ComparisonOperator.NotEqual => Math.Abs(value - CompareValue) >= 0.001f,
            _ => false
        };
    }

    public enum ComparisonOperator
    {
        Greater,          // >
        Less,             // 
        GreaterOrEqual,   // >=
        LessOrEqual,      // <=
        Equal,            // ==
        NotEqual          // !=
    }
}