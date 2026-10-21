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
    /// <summary>
    /// Gets or sets a value indicating whether DDA smoothing is enabled.
    /// </summary>
    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("DDA")]
    public bool UseDDASmoothing = true;
    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("Director")]
    public List<DirectorRule> DirectorRules = new();
    /// <summary>
    /// Gets or sets a value indicating whether Director pacing is enabled.
    /// </summary>
    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("Director")]
    public bool UseDirector = true;
    [EditorOrder(2), EditorDisplay("Config")]
    public float SmoothingSpeed = 0.1f;
}
