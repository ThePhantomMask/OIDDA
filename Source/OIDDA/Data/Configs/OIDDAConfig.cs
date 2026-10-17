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
    public List<Metrics> Metrics = new();
    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("DDA")]
    public List<Rule> Rules = new();
    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("Director")]
    public List<DirectorRule> DirectorRules = new();
    public float SmoothingSpeed = 0.1f;
}
