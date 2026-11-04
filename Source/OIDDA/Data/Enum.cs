namespace OIDDA.Data;

public enum GlobalType
{
    Single,
    Multiple
}

#region DDA enums

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

/// <summary>
/// Specifies the types of adjustment operations that can be applied to a value, such as addition, subtraction, multiplication, division, assignment, toggling a boolean, or appending a string.
/// </summary>
public enum AdjustmentOperator
{
    /// <summary>
    /// Adds two values.
    /// </summary>
    Add,
    /// <summary>
    /// Subtracts one number from another.
    /// </summary>
    Subtract,
    /// <summary>
    /// Calculates the product of two values.
    /// </summary>
    Multiply,
    /// <summary>
    /// Divides two values.
    /// </summary>
    Divide,
    /// <summary>
    /// Sets the new value
    /// </summary>
    Set,
    /// <summary>
    /// Only for the Bool type value
    /// </summary>
    Toggle,
    /// <summary>
    /// Only for the String type value
    /// </summary>
    Append
}

/// <summary>
/// Specifies the contexts in which a rule can be applied during evaluation.
/// </summary>
public enum RuleApplicationContext
{
    /// <summary>
    /// When the rule is always applied
    /// </summary>
    Always,
    /// <summary>
    /// When the rule is applied when the game is hard
    /// </summary>
    WhenTooDifficult,
    /// <summary>
    /// When the rule is applied when the game is easy
    /// </summary>
    WhenTooEasy,
    /// <summary>
    /// When the rule is applied when the game is balanced
    /// </summary>
    WhenBalanced,
    /// <summary>
    /// When the rule is applied when didn't none context
    /// </summary>
    None
}

public enum ExceptionType
{
    Tutorial,
    BossFight,
    FinalLevel,
    DirectorOverride,
    StoryMoment,
    Custom
}

public enum MetricState
{
    Good,
    Normal,
    Warning,
    Critical
}

public enum DifficultyState
{
    TooEasy,
    Balanced,
    TooDifficult
}

public enum ORSType
{
    ReceiverSender,
    Receiver,
    Sender
}

public enum ORSStatus
{
    Disconnected,
    Connected,
}

public static class ORSTypeExtensions
{
    public static bool CanReceive(this ORSType type) => type is ORSType.ReceiverSender or ORSType.Receiver;

    public static bool CanSend(this ORSType type) => type is ORSType.ReceiverSender or ORSType.Sender;
}

#endregion

#region Group enums

/// <summary>
/// How the values of the individual players are combined into one value for the group.
/// </summary>
public enum GroupAggregation
{
    /// <summary> Plain average of every player. </summary>
    Average,
    /// <summary> The highest value: the group follows its most affected player. </summary>
    Max,
    /// <summary> Average weighted by each player's group weight (<see cref="OIDDAContext.GroupWeight"/>). </summary>
    Weighted,
    /// <summary> A percentile of the players' values (see <see cref="OIDDAGroupConfig.Percentile"/>). </summary>
    Percentile
}

#endregion

#region Director enums

public enum DirectorState
{
    /// <summary> Tension build-up </summary>
    Build,
    /// <summary> Peak intensity </summary>
    Peak,
    /// <summary> Tension decrease </summary>
    Fade,
    /// <summary> Recovery/rest </summary>
    Relax
}

public enum EmotionType
{
    Stress,
    Fatigue
}

#endregion