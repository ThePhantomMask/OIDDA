using FlaxEditor.Content.Settings;
using FlaxEngine;
using FlaxEngine.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace OIDDA;

/// <summary>
/// OIDDA Manager
/// </summary>
public class OIDDAManager : Script
{
    Dictionary<string, IORSAgentD> ORSAgentDB = new();
    Dictionary<string, IORSAgentS> StaticORSDB = new();
    Dictionary<string, object> _currentMetrics = new();
    GameplayGlobals GameplayValues;
    float UpdateInterval, Delay, _timerBeforeUpdate, _timerSender;

    public override void OnStart()
    {
        var Settings = GameSettings.Load();
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
        OIDDAReset();
    }

    internal void OIDDAInit()
    {
        var OIDDA = Engine.GetCustomSettings("OIDDASettings").CreateInstance<OIDDASettings>();
        GameplayValues = OIDDA.Globals;
        if (StaticORSDB.Capacity != 0) StaticORSDB.AddRange(OIDDA.StaticORS);
        _currentMetrics.AddRange(GameplayValues.Values);
        UpdateInterval = OIDDA.UpdateInterval;
        Delay = OIDDA.Delay;
    }

    void OIDDAReset()
    {
        if (GameplayValues) GameplayValues.ResetValues(); 
        if(_currentMetrics.Capacity != 0) _currentMetrics.Clear(); 
        if (ORSAgentDB.Capacity != 0) ORSAgentDB.Clear(); 
        if (StaticORSDB.Capacity != 0) StaticORSDB.Clear();
        _timerBeforeUpdate = 0;
    }

    void MetricsUpdate()
    {
        _timerBeforeUpdate += Time.DeltaTime;

        if (_timerBeforeUpdate >= UpdateInterval)
        {
            _currentMetrics.ForEach(metric => GameplayValues.SetValue(metric.Key, metric.Value));
            Debug.Log("OIDDA metrics updated");
            _timerBeforeUpdate = 0;
        }
    }

    void OIDDAUpdate()
    {
        MetricsUpdate();
    }

    #region ORS Functions

    public bool Connect(string AgentName)
    {
        if(StaticORSDB.ContainsKey(AgentName))
        {
            if (!StaticORSDB[AgentName].IsActive)
            {
                StaticORSDB[AgentName].SetIsActive(true);
                Debug.Log($"ORS: {AgentName} Connection Status: {StaticORSDB[AgentName].IsActive}");
                return true;
            }
            Debug.Log($"{AgentName} already connected!");
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

    public bool Disconnect(string AgentName)
    {
        if (StaticORSDB.ContainsKey(AgentName))
        {
            StaticORSDB[AgentName].SetIsActive(false);
            Debug.Log($"ORS: {ID} Connection Status: {StaticORSDB[AgentName].IsActive}");
            return true;
        }
        return false;
    }

    public bool Disconnect(string ID, ORSUtils.ORSType type)
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

    public void SetStaticGlobal(string NameAgent, object value) => (Delay != 0f ? (Action)(() => DelaySender(StaticORSDB[NameAgent].GlobalVariable, value)) : () => _currentMetrics[StaticORSDB[NameAgent].GlobalVariable] = value)();

    public T GetGlobal<T>(string name) => GameplayValues.GetValue(name) is T typeValue ? typeValue : default(T);

    public T GetStaticGlobal<T>(string NameAgent) => GameplayValues.GetValue(StaticORSDB[NameAgent].GlobalVariable) is T typeValue ? typeValue : default(T);

    #endregion

    public override void OnUpdate()
    {
        OIDDAUpdate();
    }
}
