using FlaxEditor.Content.Settings;
using FlaxEngine;
using System.Collections.Generic;

namespace OIDDA;

/// <summary>
/// Multiple Globals
/// </summary>
public struct MultipleGlobals
{
    public MultipleGlobals()
    {
        Tags = GameSettings.Load<LayersAndTagsSettings>().Tags;
        PlayGlobal = null;
    }

    public List<string> Tags;
    public GameplayGlobals PlayGlobal;
}
