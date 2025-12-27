using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// OIDDA Plugin
/// </summary>
public class OIDDAPlugin : GamePlugin
{
    public OIDDAPlugin()
    {
        _description = new PluginDescription()
        {
            Name = "OIDDA",
            Category = "Other",
            Author = "The Phantom Mask",
            RepositoryUrl = "https://github.com/ThePhantomMask/OIDDA",
            Description = "A plugin that adds DDA, which intelligently and organically manages the experience in a simple, out-of-the-box way.",
            Version = new Version(0, 1, 0),
            IsAlpha = true,
            IsBeta = false,
        };
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
    }

    /// <inheritdoc/>
    public override void Deinitialize()
    {
        base.Deinitialize();
    }
}