using FlaxEngine;
using OIDDA.Data;
using System;
using System.Collections.Generic;

namespace OIDDA;

/// <summary>
/// OIDDA Rule
/// </summary>
[Category(name: "OIDDA Data")]
public class Rule : RuleBase<DDACondition, RuleException>
{
    protected override bool IsConditionMet(DDACondition condition, Dictionary<string, object> metrics) =>
        condition.IsMet(metrics);
}