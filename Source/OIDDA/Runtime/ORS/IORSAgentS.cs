using System;
using System.Collections.Generic;
using FlaxEngine;
using OIDDA.Data;

namespace OIDDA;

/// <summary>
/// Static ORS Agent struct.
/// </summary>
public struct IORSAgentS
{
    public string GlobalVariable;
    public string ORSID { get => ORSUtils.GeneratedID; }
    public ORSType ORSType;
    public ORSStatus ORSStatus => TotalORSAgentsConnected > 0 ? ORSStatus.Connected : ORSStatus.Disconnected;
    [HideInEditor] public int TotalORSAgentsConnected;
}
