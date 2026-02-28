using FlaxEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OIDDA;

/// <summary>
/// DirectorCondition class.
/// </summary>
public class DirectorCondition
{
    public List<ConditionClause> Clauses;
    public bool RequireAll = true;  // true = AND, false = OR

    public bool IsMet(Dictionary<string, object> metrics)
    {
        if (Clauses == null || Clauses.Count == 0) return true;
        return RequireAll ? Clauses.All(c => c.Evaluate(metrics)) : Clauses.Any(c => c.Evaluate(metrics));
    }
}
