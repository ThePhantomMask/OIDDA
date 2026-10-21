using OIDDA.Data;

namespace OIDDA;

/// <summary>
/// DirectorRule class.
/// </summary>
public class DirectorRule : RuleBase<DirectorCondition,DirectorRuleException>
{
    protected override bool showEmotion => true;
}
