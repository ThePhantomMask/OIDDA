using System.Collections.Generic;
using FlaxEngine;
using OIDDA.Data;

namespace OIDDA;

/// <summary>
/// OIDDA Group Config: how the players' state is combined into the group Director, and the rules that act on group-level variables.
/// </summary>
[Category(name: "OIDDA Data")]
public class OIDDAGroupConfig
{
    /// <summary> How the players' scores, intensities, stress and fatigue are combined. </summary>
    [EditorDisplay("Aggregation")]
    public GroupAggregation Aggregation = GroupAggregation.Average;

    /// <summary> Percentile used by <see cref="GroupAggregation.Percentile"/> (0 = lowest player, 1 = highest). </summary>
    [EditorDisplay("Aggregation"), VisibleIf(nameof(isPercentile)), Range(0f, 1f)]
    public float Percentile = 0.75f;

    /// <summary>
    /// Seconds a new player counts as neutral for the group (score 0.5, no intensity, stress or fatigue) before their real
    /// values are used. Only the group is affected: the player's own rules start immediately.
    /// </summary>
    [EditorDisplay("Aggregation"), Range(0f, 600f)]
    public float JoinWarmupSeconds = 10f;

    /// <summary> Turn off on a dedicated server, where the default player is not a real player. </summary>
    [EditorDisplay("Aggregation")]
    public bool IncludeDefaultPlayer = true;

    /// <summary> Seconds between group evaluations. 0 = every frame. </summary>
    [EditorDisplay("Group Director"), Range(0f, 5f)]
    public float UpdateInterval = 0.25f;

    [EditorDisplay("Group Director")]
    public bool UseSmoothing = true;

    [EditorDisplay("Group Director")]
    public float SmoothingSpeed = 0.1f;

    /// <summary>
    /// Rules of the group Director. They act on group-level variables (see <see cref="OIDDAGroup.Store"/>), and their
    /// conditions can read the Group.* variables.
    /// </summary>
    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("Group Director")]
    public List<DirectorRule> GroupRules = new();

    protected bool isPercentile => Aggregation == GroupAggregation.Percentile;
}
