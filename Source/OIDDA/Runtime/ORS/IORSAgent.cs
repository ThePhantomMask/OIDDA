using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// ORS Agent struct.
/// </summary>
public struct IORSAgent
{
    public Script ORSScript;
    public string ORSID { get => ORSUtils.GeneratedID; }
    public ORSUtils.ORSType ORSType;
}
