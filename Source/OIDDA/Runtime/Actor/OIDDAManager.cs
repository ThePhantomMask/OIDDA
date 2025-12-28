using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// OIDDA Manager Actor
/// </summary>
[ActorContextMenu("New/Other/OIDDA Manager"), ActorToolbox("Other")]
public class OIDDAManager : Actor
{
    [HideInEditor]
    public OIDDAManagerActions OMA;

    /// <inheritdoc/>
    public override void OnBeginPlay()
    {
        base.OnBeginPlay();
        OMA = this.GetScript<OIDDAManagerActions>();
        if (!OMA) OMA = this.AddScript<OIDDAManagerActions>();
    }

    /// <inheritdoc/>
    public override void OnEndPlay()
    {
        base.OnEndPlay();
        if (OMA) OMA.OIDDAReset();
    }
    
    public override void OnEnable()
    {
        base.OnEnable();
        // Here you can add code that needs to be called when Actor is enabled (eg. register for events). This is called during edit time as well.
    }

    public override void OnDisable()
    {
        base.OnDisable();
        // Here you can add code that needs to be called when Actor is disabled (eg. unregister from events). This is called during edit time as well.
    }

}
