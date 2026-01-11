using FlaxEngine;
using System;
using System.Collections.Generic;

namespace OIDDA;

/// <summary>
/// OIDDA Settings
/// </summary>
[Category(name: "OIDDA Data")]
public class OIDDASettings : SettingsBase
{
    /// <summary>
    /// List of all Gameplay Globals for the game.
    /// </summary>
    [EditorOrder(0), EditorDisplay("OIDDA Config")]
    public List<GameplayGlobals> Globals;
    /// <summary>
    /// List of OIDDA Configurations.
    /// </summary>
    [EditorOrder(0), EditorDisplay("OIDDA Config")]
    public List<JsonAssetReference<OIDDAConfig>> Configs;
    /// <summary>
    /// Metrics update interval
    /// </summary>
    [EditorOrder(0), EditorDisplay("OIDDA Config"), Tooltip("Metrics update interval (seconds)")]
    public float UpdateInterval = 1.0f;
    /// <summary>
    /// Collection of Static ORS (OIDDA Receiver Sender) agents for managing the OIDDA data.
    /// </summary>
    [EditorOrder(2), EditorDisplay("ORS Config")]
    public Dictionary<string, IORSAgentS> StaticORS;
    /// <summary>
    /// Delay for ORS Agents
    /// </summary>
    [EditorOrder(2), Range(0, 1), EditorDisplay("ORS Config")]
    public float Delay;
}
