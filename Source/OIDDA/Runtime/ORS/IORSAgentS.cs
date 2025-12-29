using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// Static ORS Agent struct.
/// </summary>
public struct IORSAgentS
{
    public string GlobalVariable;
    public string ORSID { get => ORSUtils.GeneratedID; }
    public ORSUtils.ORSType ORSType;
    public ORSUtils.ORSStatus ORSStatus => TotalORSAgentsConnected > 0 ? ORSUtils.ORSStatus.Connected : ORSUtils.ORSStatus.Disconnected;
    public int TotalORSAgentsConnected;
}
