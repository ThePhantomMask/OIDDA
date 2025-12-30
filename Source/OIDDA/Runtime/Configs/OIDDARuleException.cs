using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// OIDDA Rule Exception
/// </summary>
public class OIDDARuleException : OIDDARule
{
    public string ExceptionName;
    public ExceptionType Type;
    public new List<OIDDARuleException> Exceptions { get => null; set { } }

    public override void Apply(Dictionary<string, object> metrics)
    {
        Debug.Log($"[OIDDA] Exception '{ExceptionName}' triggered, overriding parent rule");
        base.Apply(metrics);
    }
}

public enum ExceptionType
{
    BossFight,
    Tutorial,
    FinalLevel,
    EmotionalOverride,
    StoryMoment,
    Custom
}
