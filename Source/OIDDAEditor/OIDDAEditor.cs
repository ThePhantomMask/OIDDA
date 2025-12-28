using FlaxEditor;
using FlaxEditor.Content.Settings;
using FlaxEngine;
using OIDDA;
using System.IO;

namespace OIDDAEditor;

/// <summary>
/// OIDDAEditor class.
/// </summary>
public class OIDDAEditor : EditorPlugin
{
    string _settingsPath;
    JsonAsset _jsonAsset;


    public override void InitializeEditor()
    {
        _settingsPath = Path.Combine(Globals.ProjectContentFolder, "Settings", "OIDDASettings.json");
        if (!File.Exists(_settingsPath))
        {
            FlaxEditor.Editor.SaveJsonAsset(_settingsPath, new OIDDASettings());
        }
        _jsonAsset = Engine.GetCustomSettings("OIDDASettings");
        if (!_jsonAsset)
        {
            _jsonAsset = Content.LoadAsync<JsonAsset>(_settingsPath);
            GameSettings.SetCustomSettings("OIDDASettings", _jsonAsset);
        }

        Editor.ContentDatabase.Rebuild(true);
    }
}
