using FlaxEditor.Content.Settings;
using FlaxEngine;
using FlaxEngine.Utilities;
using System.Collections.Generic;
using SimpleCoroutines;

namespace OIDDA;

/// <summary>
/// OIDDA Manager Actions
/// </summary>
public class OIDDAManagerActions : Script
{

    Dictionary<string, IORSAgentD> ORSAgentDB = new();
    Dictionary<string, IORSAgentS> StaticORSDB = new();
    GameplayGlobals GameplayValues;
    float Delay;

    public override void OnStart()
    {
        var Brain = GameSettings.Load<OIDDASettings>();
        GameplayValues = Brain.Globals;
        StaticORSDB.AddRange(Brain.StaticORS);
        Delay = Brain.Delay;
    }
    
    public override void OnEnable()
    {
        // Here you can add code that needs to be called when script is enabled (eg. register for events)
    }

    public override void OnDisable()
    {
        // Here you can add code that needs to be called when script is disabled (eg. unregister from events)
    }

    public override void OnDestroy()
    {
        GameplayValues.ResetValues();
        ORSAgentDB.Clear(); StaticORSDB.Clear();
    }

    public bool Connect(Script script, ORSUtils.ORSType type)
    {
        foreach (var ORS in StaticORSDB)
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

    public void SetGlobal(string name, object value) => SimpleCoroutine.Invoke(() => GameplayValues.SetValue(name, value), Delay, Actor);

    public T GetGlobal<T>(string name) => GameplayValues.GetValue(name) is T typeValue ? typeValue : default(T);



    public override void OnUpdate()
    {
        // Here you can add code that needs to be called every frame
    }
}
