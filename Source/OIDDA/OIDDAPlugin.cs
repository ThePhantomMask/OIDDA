using FlaxEditor.Content.Settings;
using FlaxEngine;
using OIDDA.Data;
using System;
using System.Collections.Generic;
using System.IO;

namespace OIDDA;

/// <summary>
/// OIDDA Plugin
/// </summary>
public class OIDDAPlugin : GamePlugin
{
    private static OIDDAPlugin _instance;

    /// <summary>
    /// Cached plugin instance (PluginManager lookup only happens until Initialize runs).
    /// </summary>
    public static OIDDAPlugin Instance => _instance ??= PluginManager.GetPlugin<OIDDAPlugin>();

    public OIDDASettings Settings;
    public OIDDAManager Manager;

    public GameplayGlobals CurrentGlobals;
    public List<StaticORSAgentEntry> CurrentStaticORSAgents;
    public JsonAssetReference<OIDDAConfig> CurrentOIDDAConfig;

    public OIDDAPlugin()
    {
        const string description =
            "A plugin that adds intelligent difficulty adaptation system designed to create personalised and seamless gaming experiences.\n" +
            "Simple and out-of-the-box way.\n";

        _description = new PluginDescription()
        {
            Name = "OIDDA",
            Category = "Other",
            Author = "Phantom Raptor Studio",
            RepositoryUrl = "https://github.com/ThePhantomMask/OIDDA",
            Description = description,
            Version = new Version(0, 8, 7),
            IsAlpha = false,
            IsBeta = true,
        };
    }

    private int FindIndex(Scene scene)
    {
        if (Settings.GlobalType == GlobalType.Single) return 0;
        return Settings.Globals == null ? -1 : Settings.Globals.FindIndex(mg => scene.HasTag(Tags.Get(mg.Tag)));
    }

    private static bool HasIndex<T>(List<T> list, int index) => list != null && index >= 0 && index < list.Count;

    public override void Initialize()
    {
        base.Initialize();

        _instance = this;
        var settingsAsset = Engine.GetCustomSettings("OIDDASettings");
        if (!settingsAsset)
        {
            Debug.LogError("OIDDA settings asset not found. Open Plugins > Open OIDDA Settings in the editor.");
            return;
        }

        Settings = settingsAsset.CreateInstance<OIDDASettings>();
        var settings = GameSettings.Load();
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), settings.CompanyName, settings.ProductName, Settings.FolderName);
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        
        Level.SceneLoaded += OnSceneLoaded;
    }

    public override void Deinitialize()
    {
        Level.SceneLoaded -= OnSceneLoaded;
        _instance = null;
        base.Deinitialize();
    }

    private void OnSceneLoaded(Scene currentscene, Guid guid)
    {
        // Never keep state from the previous scene.
        Manager = null;
        CurrentGlobals = null;
        CurrentStaticORSAgents = null;
        CurrentOIDDAConfig = default;

        if (Settings == null) return;

        Manager = currentscene.FindScript<OIDDAManager>();

        if (!Manager)
        {
            #if FLAX_EDITOR
                Debug.LogError("No actor with OIDDAManager script found in scene!");
            #endif
            return;
        }

        int currentIndex = FindIndex(currentscene);
        if (currentIndex < 0) return;

        bool single = Settings.GlobalType == GlobalType.Single;

        CurrentStaticORSAgents = HasIndex(Settings.StaticORSGroup, currentIndex) ? Settings.StaticORSGroup[currentIndex] : null;

        CurrentGlobals = single ? Settings.Global
            : HasIndex(Settings.Globals, currentIndex) ? Settings.Globals[currentIndex].GlobalValue : null;

        if (!CurrentGlobals)
        {
           #if FLAX_EDITOR
                Debug.LogError($"No OIDDA {(single ? "Global" : "Globals")} found.\nPlease check your OIDDA settings.");
           #endif
            return;
        }

        CurrentOIDDAConfig = single ? Settings.Config
            : HasIndex(Settings.Configs, currentIndex) ? Settings.Configs[currentIndex] : default;

        if (!CurrentOIDDAConfig)
        {
           #if FLAX_EDITOR
                Debug.LogError($"No OIDDA {(single ? "Config" : "Configs")} found.\nPlease check your OIDDA settings.");
           #endif
        }
    }
}
