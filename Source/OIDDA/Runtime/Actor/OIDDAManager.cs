using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// OIDDA Manager Actor
/// </summary>
[ActorContextMenu("New/Other/OIDDA Manager"), ActorToolbox("Other")]
public class OIDDAManager : EmptyActor
{
    Dictionary<string, IORSAgentD> ORSAgentDB = new();
    Dictionary<string, IORSAgentS> StaticORSDB = new();
    GameplayGlobals GameplayValues;

    /// <inheritdoc/>
    public override void OnBeginPlay()
    {
        base.OnBeginPlay();
        // Here you can add code that needs to be called when Actor added to the game. This is called during edit time as well.
    }

    /// <inheritdoc/>
    public override void OnEndPlay()
    {
        base.OnEndPlay();
        ORSAgentDB.Clear(); StaticORSDB.Clear();
    }
    
    /// <inheritdoc/>
    public override void OnEnable()
    {
        base.OnEnable();
        // Here you can add code that needs to be called when Actor is enabled (eg. register for events). This is called during edit time as well.
    }

    /// <inheritdoc/>
    public override void OnDisable()
    {
        base.OnDisable();
        // Here you can add code that needs to be called when Actor is disabled (eg. unregister from events). This is called during edit time as well.
    }

    public bool Connect(Script script, ORSUtils.ORSType type)
    {
        foreach(var ORS in StaticORSDB)
        {
            if (ORS.Value.ORSScript == script && ORS.Value.ORSType == type)
            {
                ORS.Value.SetIsActive(true);
                Debug.Log($"ORS: {ORS.Key} Connection Status: {ORS.Value.IsActive}");
                return true;
            }
        }
        return false;
    }

    public bool Connect(string ID, IORSAgentD agentD)
    {
        if (!ORSAgentDB.ContainsKey(ID))
        {
            ORSAgentDB.Add(ID, agentD);
            return true;
        }
        return false;
    }

    public bool Disconnect(Script script)
    {
        foreach (var ORS in StaticORSDB)
        {
            if (ORS.Value.ORSScript == script)
            {
                ORS.Value.SetIsActive(false);
                Debug.Log($"ORS: {ORS.Key} Connection Status: {ORS.Value.IsActive}");
                return true;
            }
        }
        return false; 
    }

    public bool Disconnect(string ID)
    {
        if (ORSAgentDB.ContainsKey(ID))
        {
            ORSAgentDB.Remove(ID);
            return true;
        }
        return false;
    }

}
