using FlaxEditor.Content.Settings;
using FlaxEngine;
using OIDDA.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OIDDA;

/// <summary>
/// OIDDA Plugin
/// </summary>
public class OIDDAPlugin : GamePlugin
{
    public static OIDDAPlugin Instance { get => PluginManager.GetPlugin<OIDDAPlugin>(); }

    public OIDDASettings Settings;
    public OIDDAManager Manager;

    public GameplayGlobals CurrentGlobals;
    public List<StaticORSAgentEntry> CurrentStaticORSAgents;
    public JsonAssetReference<OIDDAConfig> CurrentOIDDAConfig;

    private StringBuilder _logTextBuilder;

    public OIDDAPlugin()
    {
        var description = new StringBuilder();
        description.AppendLine("A plugin that adds intelligent difficulty adaptation system designed to create personalised and seamless gaming experiences.");
        description.AppendLine("Simple and out-of-the-box way.");

        _description = new PluginDescription()
        {
            Name = "OIDDA",
            Category = "Other",
            Author = "Phantom Raptor Studio",
            RepositoryUrl = "https://github.com/ThePhantomMask/OIDDA",
            Description = description.ToString(),
            Version = new Version(0, 0, 9250),
            IsAlpha = false,
            IsBeta = true,
        };
    }

    private int FindIndex(Scene scene) => Settings.GlobalType == GlobalType.Single ? 0
        : Settings.Globals.FindIndex(mg => scene.HasTag(Tags.Get(mg.Tag)));

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

    private void OnSceneLoaded(Scene currentscene, Guid guid)
    {
        Manager = currentscene.FindScript<OIDDAManager>();

        if (!Manager)
        {
            #if FLAX_EDITOR
                _logTextBuilder = new StringBuilder();
                _logTextBuilder.AppendLine("No actor with OIDDAManager script found in scene!");
                Debug.LogError(_logTextBuilder.ToString());
            #endif
            return;
        }

        int currentIndex = FindIndex(currentscene);

        if (currentIndex >= 0)
        {
            CurrentStaticORSAgents = Settings.StaticORSGroup[currentIndex];

            CurrentGlobals = Settings.GlobalType == GlobalType.Single ? Settings.Global : Settings.Globals[currentIndex].GlobalValue;

            if (!CurrentGlobals)
            {
               #if FLAX_EDITOR
                    string globalType = Settings.GlobalType == GlobalType.Single ? "Global" : "Globals";
                    _logTextBuilder = new StringBuilder();
                    _logTextBuilder.AppendLine($"No OIDDA {globalType} found.");
                    _logTextBuilder.AppendLine("Please check your OIDDA settings.");
                    Debug.LogError(_logTextBuilder.ToString());
               #endif
                return;
            }

            CurrentOIDDAConfig = Settings.GlobalType == GlobalType.Single ? Settings.Config : Settings.Configs[currentIndex];

            if (!CurrentOIDDAConfig)
            {
               #if FLAX_EDITOR
                    string configType = Settings.GlobalType == GlobalType.Single ? "Config" : "Configs";
                    _logTextBuilder = new StringBuilder();
                    _logTextBuilder.AppendLine($"No OIDDA {configType} found.");
                    _logTextBuilder.AppendLine("Please check your OIDDA settings.");
                    Debug.LogError(_logTextBuilder.ToString());
               #endif
                return;
            }
        }
    }
}