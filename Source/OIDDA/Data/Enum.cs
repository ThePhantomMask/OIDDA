using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA.Data;

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

public enum DirectorCategory
{
    Stress,
    Fatigue
}

public enum DirectorAction
{
    Increase, 
    Decrease
}

#endregion