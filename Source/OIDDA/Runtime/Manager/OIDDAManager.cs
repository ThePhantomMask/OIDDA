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
    public Dictionary<string, IORSAgentS> StaticORSDB = new();
    public Dictionary<string, object> _currentMetrics = new(), _previousMetrics = new();
    GameplayGlobals GameplayValues;
    float UpdateInterval, Delay, _timerBeforeUpdate, _timerSender, _timerReceiver;

    bool InstantMetricsUpdated;

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
        if (OIDDA != null)
        {
            GameplayValues = OIDDA.Globals;
            StaticORSDB = OIDDA.StaticORS.DeepClone();
            _currentMetrics = GameplayValues.Values.DeepClone();
            UpdateInterval = OIDDA.UpdateInterval;
            Delay = OIDDA.Delay;
        }
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

        if (_timerBeforeUpdate >= UpdateInterval || InstantMetricsUpdated)
        {
            _currentMetrics.ForEach(metric => GameplayValues.SetValue(metric.Key, metric.Value));
            Debug.Log("OIDDA metrics updated");
            InstantMetricsUpdated = false; _timerBeforeUpdate = 0;
            _previousMetrics = _currentMetrics.DeepClone();
        }
    }

    void OIDDAUpdate()
    {
        MetricsUpdate();
    }

    #region ORS Agent Management

    public bool Connect(string AgentName)
    {
        if(StaticORSDB.ContainsKey(AgentName))
        {
            if (!StaticORSDB[AgentName].IsActive)
            {
                StaticORSDB[AgentName].SetIsActive(true);
                Debug.Log($"{AgentName} is connected! ");
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
            Debug.Log($"{AgentName} is disconnected !");
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

    T DelayReceiver<T>(string name)
    {
        _timerReceiver += Time.DeltaTime;
        if (_timerReceiver >= Delay)
        {
            _timerReceiver = 0;
            return GameplayValues.GetValue(name) is T typeValue ? typeValue : default(T);
        }
        return default(T);
    }

    public bool VerifyIsReceiver(string ID) => ORSAgentDB[ID].ORSType == ORSUtils.ORSType.ReceiverSender || ORSAgentDB[ID].ORSType == ORSUtils.ORSType.Receiver;

    public bool VerifyIsStaticReceiver(string Name) => StaticORSDB[Name].ORSType == ORSUtils.ORSType.ReceiverSender || StaticORSDB[Name].ORSType == ORSUtils.ORSType.Receiver;

    public bool VerifyIsSender(string ID) => ORSAgentDB[ID].ORSType == ORSUtils.ORSType.ReceiverSender || ORSAgentDB[ID].ORSType == ORSUtils.ORSType.Sender;

    public bool VerifyIsStaticSender(string Name) => StaticORSDB[Name].ORSType == ORSUtils.ORSType.ReceiverSender || StaticORSDB[Name].ORSType == ORSUtils.ORSType.Sender;

    public void SetGlobal(string name, object value) => (Delay != 0f ? (Action)(() => DelaySender(name, value)) : () => _currentMetrics[name] = value)();

    public void SetStaticGlobal(string NameAgent, object value) => (Delay != 0f ? (Action)(() => DelaySender(StaticORSDB[NameAgent].GlobalVariable, value)) : () => _currentMetrics[StaticORSDB[NameAgent].GlobalVariable] = value)();

    public T GetGlobal<T>(string name) => (Delay != 0f) ? DelayReceiver<T>(name) : GameplayValues.GetValue(name) is T typeValue ? typeValue : default(T);

    public T GetStaticGlobal<T>(string NameAgent) => (Delay != 0f) ? DelayReceiver<T>(StaticORSDB[NameAgent].GlobalVariable) : GameplayValues.GetValue(StaticORSDB[NameAgent].GlobalVariable) is T typeValue ? typeValue : default(T);

    #endregion

    public override void OnUpdate()
    {
        OIDDAUpdate();
    }
}
