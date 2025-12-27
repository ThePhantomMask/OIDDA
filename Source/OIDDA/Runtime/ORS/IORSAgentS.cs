using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// Static ORS Agent struct.
/// </summary>
public struct IORSAgentS
{
    public Script ORSScript;
    public string ORSID { get => ORSUtils.GeneratedID; }
    public ORSUtils.ORSType ORSType;
    public bool IsActive { get; private set; }

    public void SetIsActive(bool status) => IsActive = status;

}
