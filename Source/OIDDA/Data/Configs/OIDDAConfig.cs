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
    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("DDA")]
    public List<DDAMetrics> Metrics = new();
    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("DDA")]
    public List<Rule> Rules = new();
    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("Director")]
    public List<DirectorMetrics> DirectorMetrics = new();
    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("Director")]
    public List<DirectorRule> DirectorRules = new();
    public float SmoothingSpeed = 0.1f;
}
