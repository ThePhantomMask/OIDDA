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
    /// The file name to use.
    /// </summary>
    [EditorOrder(1), EditorDisplay("File Config")]
    public string FolderName;
    /// <summary>
    /// Whether to encrypt the save data.
    /// </summary>
    [EditorOrder(1), EditorDisplay("File Config")]
    public bool UseEncryption = false;
    /// <summary>
    /// The password used for encryption.
    /// </summary>
    [EditorOrder(1), EditorDisplay("File Config")]
    public string Password = "password";
    /// <summary>
    /// Whether to add and use a hash to check for file changes.
    /// </summary>
    [EditorOrder(1), EditorDisplay("File Config")]
    public bool UseHash = true;
    /// <summary>
    /// Whether to use verbose logging. Automatically disabled in release builds.
    /// </summary>
    [EditorOrder(1), EditorDisplay("File Config")]
    public bool VerboseLogging = true;

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
