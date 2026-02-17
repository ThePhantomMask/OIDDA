using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// Rule Exception class for the OIDDA system.
/// </summary>
[Category(name: "OIDDA Data")]
public class RuleException : Rule
{
    public string RuleExceptionName;
    public ExceptionType RuleType;

    public new List<RuleException> Exceptions;

    /// <summary>
    ///  Overrides the Apply method to include logging when an exception is triggered.
    /// </summary>
    /// <param name="metrics">Metrics that will be handled, controlled</param>
    public override void Apply(Dictionary<string, object> metrics)
    {
        Debug.Write(LogType.Info, $"Exception rule {RuleExceptionName} triggered");
        base.Apply(metrics);
    }
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
