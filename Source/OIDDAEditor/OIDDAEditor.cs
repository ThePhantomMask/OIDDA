using FlaxEditor;
using FlaxEditor.Content;
using FlaxEditor.Content.Settings;
using FlaxEngine;
using OIDDA;
using System.IO;

namespace OIDDAEditor;

/// <summary>
/// OIDDA Editor
/// </summary>
public class OIDDAEditor : EditorPlugin
{
    string _settingsPath, _settingName= "OIDDASettings";
    JsonAsset _jsonAsset;
    CustomSettingsProxy _settingsProxy;

    public override void InitializeEditor()
    {
        base.InitializeEditor();

        _settingsPath = Path.Combine(Globals.ProjectContentFolder, "Settings", $"{_settingName}.json");
        if (!File.Exists(_settingsPath))
        {
            Editor.SaveJsonAsset(_settingsPath, new OIDDASettings());
        }
        _jsonAsset = Engine.GetCustomSettings(_settingName);
        if (!_jsonAsset)
        {
            _jsonAsset = Content.LoadAsync<JsonAsset>(_settingsPath);
            GameSettings.SetCustomSettings(_settingName, _jsonAsset);
        }

        _settingsProxy = new CustomSettingsProxy(typeof(OIDDASettings), _settingName);
        Editor.ContentDatabase.AddProxy(_settingsProxy);

        Editor.ContentDatabase.Rebuild(true);
    }

    public override void DeinitializeEditor()
    {
        Editor.ContentDatabase.RemoveProxy(_settingsProxy);

        base.DeinitializeEditor();
    }

}
