using FlaxEngine;
using OIDDA.Data;
using System;
using System.Collections.Generic;

namespace OIDDA;

/// <summary>
/// DirectorRuleException class.
/// </summary>
public class DirectorRuleException : DirectorRule
{
    public string RuleExceptionName;
    public ExceptionType RuleType;

    public new List<RuleException> RuleExceptions;

    protected override bool FilterValuesByScore => false;
}
