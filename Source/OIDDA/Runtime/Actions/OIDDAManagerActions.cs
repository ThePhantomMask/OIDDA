using FlaxEditor.Content.Settings;
using FlaxEngine;
using FlaxEngine.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OIDDA;

/// <summary>
/// OIDDA Manager Actions
/// </summary>
public class OIDDAManagerActions : Script
{
    [Tooltip("Metrics update interval (seconds)")]
    public float UpdateInterval = 1f;

    GameSettings Settings;
    Dictionary<string, IORSAgentD> ORSAgentDB = new();
    Dictionary<string, IORSAgentS> StaticORSDB = new();
    Dictionary<string, object> _currentMetrics = new();
    GameplayGlobals GameplayValues;
    float Delay, _timerBeforeUpdate, _timerSender;

    public override void OnStart()
    {
        Settings = GameSettings.Load();
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Settings.CompanyName, Settings.ProductName, "OIDDA");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        OIDDAInit();
    }
    
    public override void OnEnable()
    {
        // Here you can add code that needs to be called when script is enabled (eg. register for events)
    }

    public override void OnDisable()
    {
        // Here you can add code that needs to be called when script is disabled (eg. unregister from events)
    }

    internal void OIDDAInit()
    {
        var OIDDA = Settings.CustomSettings["OIDDA"].CreateInstance<OIDDASettings>();
        GameplayValues = OIDDA.Globals;
        StaticORSDB.AddRange(OIDDA.StaticORS);
        _currentMetrics.AddRange(GameplayValues.Values);
        Delay = OIDDA.Delay;
    }

    public void OIDDAReset()
    {
        GameplayValues.ResetValues(); _timerBeforeUpdate = 0;
        _currentMetrics.Clear(); ORSAgentDB.Clear(); StaticORSDB.Clear();
    }

    void OIDDAUpdate()
    {
        _timerBeforeUpdate += Time.DeltaTime;

        if (_timerBeforeUpdate >= UpdateInterval)
        {
            _currentMetrics.ForEach(metric => GameplayValues.SetValue(metric.Key, metric.Value));
            Debug.Log("OIDDA metrics updated");
            _timerBeforeUpdate = 0;
        }
    }

    #region ORS Functions

    string NameORSAgent(Script script = null) => StaticORSDB.FirstOrDefault(kvp => kvp.Value.ORSScript == script).Key;

    public string NameORSAgent(Script script, ORSUtils.ORSType type) => 
        StaticORSDB.FirstOrDefault(kvp => kvp.Value.ORSScript == script && kvp.Value.ORSType == type).Key;

    public bool Connect(string NameStatic)
    {
        if(StaticORSDB.ContainsKey(NameStatic))
        {
            StaticORSDB[NameStatic].SetIsActive(true);
            Debug.Log($"ORS: {NameStatic} Connection Status: {StaticORSDB[NameStatic].IsActive}");
            return true; 
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
        var name = NameORSAgent(script);
        if (StaticORSDB.ContainsKey(name))
        {
            StaticORSDB[name].SetIsActive(true);
            Debug.Log($"ORS: {name} Connection Status: {StaticORSDB[name].IsActive}");
            return true;
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

    public bool ORSIsConnected(string ID = "") => ORSAgentDB.ContainsKey(ID);

    public bool ORSIsConnected() => StaticORSDB.Values.Any(agent => agent.IsActive is true);

    void DelaySender(string name, object value)
    {
        _timerSender += Time.DeltaTime;
        if (_timerSender >= Delay)
        {
            _currentMetrics[name] = value;
            _timerSender = 0;
        }
    }

    public bool VerifyIsReceiver(string ID) => ORSAgentDB[ID].ORSType == ORSUtils.ORSType.ReceiverSender || ORSAgentDB[ID].ORSType == ORSUtils.ORSType.Receiver;

    public bool VerifyIsReceiver() => StaticORSDB.Values.Any(agent => agent.ORSType == ORSUtils.ORSType.ReceiverSender || agent.ORSType == ORSUtils.ORSType.Receiver);

    public bool VerifyIsSender(string ID) => ORSAgentDB[ID].ORSType == ORSUtils.ORSType.ReceiverSender || ORSAgentDB[ID].ORSType == ORSUtils.ORSType.Sender;

    public bool VerifyIsSender() => StaticORSDB.Values.Any(agent => agent.ORSType == ORSUtils.ORSType.ReceiverSender || agent.ORSType == ORSUtils.ORSType.Sender);

    public void SetGlobal(string name, object value) => (Delay != 0f ? (Action)(() => DelaySender(name, value)) : () => _currentMetrics[name] = value)();

    public T GetGlobal<T>(string name) => GameplayValues.GetValue(name) is T typeValue ? typeValue : default(T);

    #endregion

    public override void OnUpdate()
    {
        OIDDAUpdate();
    }
}
