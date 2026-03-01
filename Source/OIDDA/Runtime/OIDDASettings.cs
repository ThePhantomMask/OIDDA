using FlaxEngine;
using System;
using System.Collections.Generic;
using OIDDA.Data;

namespace OIDDA;

/// <summary>
/// OIDDA Settings
/// </summary>
[Category(name: "OIDDA Data")]
public class OIDDASettings : SettingsBase
{
    bool isSingle => GlobalType == GlobalType.Single;
    bool isMultiple => GlobalType == GlobalType.Multiple;

    /// <summary>
    /// Select the mode.
    /// </summary>
    [EditorOrder(0), EditorDisplay("OIDDA Config")]
    public GlobalType GlobalType;
    /// <summary>
    /// Folder name
    /// </summary>
    [EditorOrder(0), EditorDisplay("OIDDA Config")]
    public string FolderName = "OIDDA";
    /// <summary>
    /// Gameplay Globals for the game.
    /// </summary>
    [VisibleIf(nameof(isSingle)), EditorOrder(1), EditorDisplay("DDA Config")]
    public GameplayGlobals Global;
    /// <summary>
    /// List of all Gameplay Globals for the game.
    /// </summary>
    [VisibleIf(nameof(isMultiple)) , EditorOrder(1), EditorDisplay("DDA Config")]
    public List<GameplayGlobals> Globals;
    /// <summary>
    /// OIDDA Configuration.
    /// </summary>
    [VisibleIf(nameof(isSingle)), EditorOrder(1), EditorDisplay("DDA Config")]
    public JsonAssetReference<OIDDAConfig> Config;
    /// <summary>
    /// List of OIDDA Configurations.
    /// </summary>
    [VisibleIf(nameof(isMultiple)), EditorOrder(1), EditorDisplay("DDA Config")]
    public List<JsonAssetReference<OIDDAConfig>> Configs;
    /// <summary>
    /// Gets or sets a value indicating whether DDA smoothing is enabled.
    /// </summary>
    [EditorOrder(1), EditorDisplay("DDA Config")]
    public bool UseDDASmoothing = true;
    /// <summary>
    /// Metrics update interval
    /// </summary>
    [EditorOrder(1), EditorDisplay("DDA Config"), Tooltip("Metrics update interval (seconds)")]
    public float UpdateInterval = 1.0f;
    /// <summary>
    /// Gets or sets a value indicating whether Director pacing is enabled.
    /// </summary>
    [EditorOrder(2), EditorDisplay("Director Config")]
    public bool UseDirector = true;
    /// <summary>
    /// Collection of Static ORS (OIDDA Receiver Sender) agents for managing the OIDDA data.
    /// </summary>
    [EditorOrder(3), EditorDisplay("ORS Config")]
    public List<Dictionary<string, IORSAgentS>> StaticORSGroup;
    /// <summary>
    /// Delay for ORS Agents
    /// </summary>
    [EditorOrder(2), Range(0, 1), EditorDisplay("ORS Config")]
    public float Delay;
}
