using OIDDA.Data;
using System.Collections.Generic;

namespace OIDDA;

/// <summary>
/// DirectorRule class.
/// </summary>
public class DirectorRule : RuleBase<DirectorCondition,DirectorRuleException>
{
    protected override bool showEmotion => true;

    protected override bool IsConditionMet(DirectorCondition condition, Dictionary<string, object> metrics) =>
        condition.IsMet(metrics);
}
