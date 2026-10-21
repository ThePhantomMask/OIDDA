using FlaxEngine;
using OIDDA.Data;
using System.Collections.Generic;

namespace OIDDA;

/// <summary>
/// Rule Exception class for the OIDDA system.
/// </summary>
[Category(name: "OIDDA Data")]
public class RuleException : Rule
{
    public string RuleExceptionName;
    public ExceptionType RuleType;

    public override List<RuleException> RuleExceptions { get; set; }

    protected override bool FilterValuesByScore => false;
}
