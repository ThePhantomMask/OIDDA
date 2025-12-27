using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// OIDDA Settings
/// </summary>
public class OIDDASettings
{
    [EditorOrder(0), EditorDisplay("OIDDA Config")]
    public GameplayGlobals Globals;

    [EditorOrder(1), EditorDisplay("ORS Config")]
    public Dictionary<string, IORSAgentS> StaticORS;
    [EditorOrder(1), Range(0,1) , EditorDisplay("ORS Config")]
    public float Delay;
}
