using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// OIDDA Config
/// </summary>
[Category(name: "OIDDA Data")]
public class OIDDAConfig
{
    public List<DDAMetrics> Metrics = new();
    public List<Rule> Rules = new();
    public Dictionary<string, DirectorValue> DirectorData = new();
    public float SmoothingSpeed = 0.1f;
}
