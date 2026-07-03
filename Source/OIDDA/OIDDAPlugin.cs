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
    public static OIDDAPlugin Instance { get => PluginManager.GetPlugin<OIDDAPlugin>(); }

    public OIDDASettings Settings;

    public GameplayGlobals CurrentGlobals;
    public List<StaticORSAgentEntry> CurrentStaticORSAgents;
    public JsonAssetReference<OIDDAConfig> CurrentOIDDAConfig;

    public OIDDAPlugin()
    {
        _description = new PluginDescription()
        {
            Name = "OIDDA",
            Category = "Other",
            Author = "Phantom Raptor Studio",
            RepositoryUrl = "https://github.com/ThePhantomMask/OIDDA",
            Description = "A plugin that adds intelligent difficulty adaptation system designed to create personalised and seamless gaming experiences in a simple, out-of-the-box way.",
            Version = new Version(0, 0, 9250),
            IsAlpha = false,
            IsBeta = true,
        };
    }

    int FindIndex(Scene scene)
    {
        var sceneTags = scene.Tags;
        return Settings.Globals.FindIndex(mg => mg.Tags.Exists(tag => tag.Contains(tag)));
    }

    public override void Initialize()
    {
        base.Initialize();

        Settings = Engine.GetCustomSettings("OIDDASettings").CreateInstance<OIDDASettings>();
        var settings = GameSettings.Load();
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), settings.CompanyName, settings.ProductName, Settings.FolderName);
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        
        Level.SceneLoaded += OnSceneLoaded;
    }

    public override void Deinitialize()
    {
        Level.SceneLoaded -= OnSceneLoaded;
        base.Deinitialize();
    }

    void OnSceneLoaded(Scene currentscene, Guid guid)
    {
        int currentIndex = (FindIndex(currentscene) != -1) ? FindIndex(currentscene) : 0;

        CurrentStaticORSAgents = Settings.StaticORSGroup[currentIndex];

        if(Settings.Global == null || Settings.Globals[currentIndex].PlayGlobal == null)
        {
          #if FLAX_EDITOR
            string configType = Settings.GlobalType == GlobalType.Single ? "Global" : "Globals";
            Debug.LogError($"No OIDDA {configType} found. \n Please check your OIDDA settings.");
          #endif
            return;
        }

        CurrentGlobals = Settings.GlobalType == GlobalType.Single ? Settings.Global : Settings.Globals[currentIndex].PlayGlobal;
        
        if (Settings.Config == null || Settings.Configs == null)
        {
          #if FLAX_EDITOR
            string configType = Settings.GlobalType == GlobalType.Single ? "Config" : "Configs";
            Debug.LogError($"No OIDDA {configType} found. \n Please check your OIDDA settings.");
          #endif
            return;
        }
        
        CurrentOIDDAConfig = Settings.GlobalType == GlobalType.Single ? Settings.Config : Settings.Configs[currentIndex];
    }
}