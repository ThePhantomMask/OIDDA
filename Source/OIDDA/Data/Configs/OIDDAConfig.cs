using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// OIDDA Config
/// </summary>
public class OIDDAConfig
{
    public List<OIDDAMetrics> Metrics = new();
    public List<OIDDARule> Rules = new();
    public float SmoothingSpeed = 0.1f;
}
