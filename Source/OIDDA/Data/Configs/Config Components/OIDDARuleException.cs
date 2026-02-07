using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// OIDDA Rule Exception
/// </summary>
[Category(name: "OIDDA Data")]
public class OIDDARuleException : OIDDARule
{
    public string ExceptionName;
    public ExceptionType Type;

    [VisibleIf(nameof(_neverShow))] public new string RuleName;
    [NoSerialize] public new List<OIDDARuleException> Exceptions;

    bool _neverShow = false;

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
