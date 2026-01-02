using System;
using System.Collections.Generic;
using System.Linq;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// OIDDA Condition
/// </summary>
[Category(name: "OIDDA Data")]
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
    public GameplayValue CompareValue;

    public bool Evaluate(Dictionary<string, object> metrics)
    {
        if (!metrics.ContainsKey(MetricName)) return false;

        var metricValue = GameplayValue.FromObject(metrics[MetricName]);
        return GameplayValueOperations.Compare(metricValue, CompareValue, Operator);
    }

    public enum ComparisonOperator
    {
        Greater,          // >
        Less,             // 
        GreaterOrEqual,   // >=
        LessOrEqual,      // <=
        Equal,            // ==
        NotEqual,         // !=
        Contains
    }
}