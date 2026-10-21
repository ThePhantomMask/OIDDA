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
    /// When off (default) the Director follows <see cref="UseDDASmoothing"/>, as it always did.
    /// Turn it on to give the Director its own smoothing setting.
    /// </summary>
    [EditorDisplay("Director")]
    public bool OverrideDirectorSmoothing = false;
    /// <summary>
    /// Director smoothing, used only when <see cref="OverrideDirectorSmoothing"/> is on.
    /// </summary>
    [EditorDisplay("Director"), VisibleIf(nameof(OverrideDirectorSmoothing))]
    public bool UseDirectorSmoothing = true;
    /// <summary>
    /// Gets or sets a value indicating whether Director pacing is enabled.
    /// </summary>
    [Collection(Display = CollectionAttribute.DisplayType.Header), EditorDisplay("Director")]
    public bool UseDirector = true;
    [EditorOrder(2), EditorDisplay("Config")]
    public float SmoothingSpeed = 0.1f;
}
