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
            Category = "Utility",
            Author = "The Phantom Mask",
            RepositoryUrl = "https://github.com/ThePhantomMask/OIDDA",
            Description = "",
            Version = new Version(0, 0, 1),
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