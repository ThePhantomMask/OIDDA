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
    public List<OIDDAMetrics> Metrics = new();
    public List<Rule> Rules = new();
    public float SmoothingSpeed = 0.1f;
}
