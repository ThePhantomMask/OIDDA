using FlaxEngine;
using OIDDA.Data;
using OIDDA.Elo;
using System;
using System.Collections.Generic;

namespace OIDDA;

/// <summary>
/// Represents an abstract base class for an ORS (OIDDA Receiver Sender) agent, providing methods for connecting, disconnecting, and exchanging values with an ORS system.
/// </summary>
/// <remarks>This class defines the core contract for interacting with ORS agents, including connection management and value transmission. 
/// Derived classes should implement the abstract connection methods to provide specific ORS agent behaviors. 
/// Thread safety and connection state management are the responsibility of the implementing class.</remarks>
public abstract class ORSAgent
{
    #region DDA API methods

    public abstract void ConnectORSAgent(string AgentName);

    public abstract void ConnectORSAgent(ORSType type);

    public abstract void DisconnectORSAgent();

    public abstract void DisconnectORSAgent(ORSType type);

    public abstract bool TryReceiverValue<T>(string nameValue, out T result);

    public abstract bool TryReceiverValue<T>(out T result);

    public abstract T ReceiverValue<T>(string nameValue);

    public abstract T ReceiverValue<T>();

    public abstract T QuickReceiver<T>(string NameValue);

    public abstract bool TrySenderValue(string nameValue, object senderValue);

    public abstract bool TrySenderValue(object senderValue);

    public abstract void SenderValue(string nameValue, object senderValue);

    public abstract void SenderValue(object senderValue);

    public abstract void QuickSender(string nameValue, object senderValue);

    #endregion

    #region  Director API methods
    public abstract void AddDirectorIntensity(float amount, string reason = "");
    #endregion
}

/// <summary>
///  OIDDA Receiver Sender Agent
/// </summary>
[Category(name: "OIDDA")]
public class ORS : ORSAgent
{
    public static ORS Instance = new();

    #region DDA API methods

    string ORSID, ORSName;
    public bool IsConnected => !string.IsNullOrEmpty(ORSID) && OIDDAUtils.OIDDAManager.ORSIsConnected(ORSID) || !string.IsNullOrEmpty(ORSName) && OIDDAUtils.OIDDAManager.StaticORSIsConnected(ORSName);

    /// <summary>
    /// Initializes the ORS agent connection using the specified script and agent type (Static ORS Agent).
    /// </summary>
    /// <param name="AgentName">The script instance that defines the connection parameters and logic for the ORS agent.</param>
    public override void ConnectORSAgent(string AgentName)
    {
        if (!OIDDAUtils.OIDDAManager) return;
        OIDDAUtils.OIDDAManager.Connect(ORSName = AgentName);
    }

    /// <summary>
    /// Initializes the ORS agent connection using the specified ORS type (Dynamic ORS Agent).
    /// </summary>
    /// <param name="type">The ORS type to use for the agent connection. Determines the configuration and behavior of the agent.</param>
    public override void ConnectORSAgent(ORSType type)
    {
        if (!OIDDAUtils.OIDDAManager) return;
        OIDDAUtils.OIDDAManager.Connect(ORSID = ORSUtils.GeneratedID, new IORSAgentD
        {
            ORSType = type
        });
    }

    /// <summary>
    ///  Disconnects the specified ORS agent represented by the provided script (Static ORS Agent).
    /// </summary>
    public override void DisconnectORSAgent()
    {
        if (!OIDDAUtils.OIDDAManager) return;
        OIDDAUtils.OIDDAManager.Disconnect(ORSName);
    }

    /// <summary>
    /// Disconnects the ORS agent from the current session (Dynamic ORS Agent).
    /// </summary>
    /// /// <param name="type">The type of ORS agent to connect to. Specifies the agent category or behavior.</param>
    public override void DisconnectORSAgent(ORSType type)
    {
        if (!OIDDAUtils.OIDDAManager) return;
        OIDDAUtils.OIDDAManager.Disconnect(ORSID, type);
    }

    public override bool TryReceiverValue<T>(string nameValue, out T result)
    {
        if (!(OIDDAUtils.OIDDAManager || IsConnected && OIDDAUtils.OIDDAManager.VerifyIsReceiver(ORSID))) 
        {
            result = default; return false; 
        }
        result = OIDDAUtils.OIDDAManager.GetGlobal<T>(nameValue); return true;
    }

    public override bool TryReceiverValue<T>(out T result)
    {
        if (!(OIDDAUtils.OIDDAManager || IsConnected && OIDDAUtils.OIDDAManager.VerifyIsStaticReceiver(ORSName))) 
        {
            result = default; return false; 
        }
        result = OIDDAUtils.OIDDAManager.GetStaticGlobal<T>(ORSName); return true;
    }

    public override T QuickReceiver<T>(string NameValue)
    {
        if (!OIDDAUtils.OIDDAManager) throw new InvalidOperationException("OIDDA Manager instance is not available.");
        return OIDDAUtils.OIDDAManager.QuickReceiver<T>(NameValue);
        throw new InvalidCastException($"Value for static receiver '{ORSName}' is not of type {typeof(T).Name}");
    }

    public override T ReceiverValue<T>()
    {
        if (!(OIDDAUtils.OIDDAManager || IsConnected && OIDDAUtils.OIDDAManager.VerifyIsStaticReceiver(ORSName))) throw new InvalidOperationException("OIDDA Manager instance is not available or ORS is not connected.");
        return OIDDAUtils.OIDDAManager.GetStaticGlobal<T>(ORSName);
        throw new InvalidCastException($"Value for static receiver '{ORSName}' is not of type {typeof(T).Name}");
    }

    public override T ReceiverValue<T>(string nameValue)
    {
        if (!(OIDDAUtils.OIDDAManager || IsConnected && OIDDAUtils.OIDDAManager.VerifyIsReceiver(ORSID))) throw new InvalidOperationException("OIDDA Manager instance is not available or ORS is not connected.");
        return OIDDAUtils.OIDDAManager.GetGlobal<T>(nameValue);
        throw new InvalidCastException($"Value for key '{nameValue}' is not of type {typeof(T).Name}");
    }

    public override bool TrySenderValue(string nameValue, object senderValue)
    {
        if (!(OIDDAUtils.OIDDAManager || IsConnected && OIDDAUtils.OIDDAManager.VerifyIsSender(ORSID))) return false;
        OIDDAUtils.OIDDAManager.SetGlobal(nameValue, senderValue);
        return true;
    }

    public override bool TrySenderValue(object senderValue)
    {
        if (!(OIDDAUtils.OIDDAManager || IsConnected && OIDDAUtils.OIDDAManager.VerifyIsStaticSender(ORSName))) return false;
        OIDDAUtils.OIDDAManager.SetStaticGlobal(ORSName, senderValue);
        return true;
    }

    public override void SenderValue(string nameValue, object senderValue)
    {
        if (!(OIDDAUtils.OIDDAManager || IsConnected && OIDDAUtils.OIDDAManager.VerifyIsSender(ORSID))) return;
        OIDDAUtils.OIDDAManager.SetGlobal(nameValue, senderValue);
    }

    public override void SenderValue(object senderValue)
    {
        if (!(OIDDAUtils.OIDDAManager || IsConnected && OIDDAUtils.OIDDAManager.VerifyIsStaticSender(ORSName))) return;
        OIDDAUtils.OIDDAManager.SetStaticGlobal(ORSName, senderValue);
    }

    public override void QuickSender(string nameValue, object senderValue)
    {
        if (!OIDDAUtils.OIDDAManager) return;
        OIDDAUtils.OIDDAManager.QuickSender(nameValue, senderValue);
    }

    #endregion

    #region  Director API methods

    /// <summary>
    /// Adds the specified amount of pacing intensity to the pacing director, optionally providing a reason for the adjustment.
    /// </summary>
    /// <param name="amount">The amount of pacing intensity to add. Positive values increase pacing intensity.</param>
    /// <param name="reason">An optional description of the reason for the intensity adjustment. This value may be used for logging or debugging purposes.</param>
    public override void AddDirectorIntensity(float amount, string reason = "")
    {
        if (!OIDDAUtils.OIDDAManager) return;
        OIDDAUtils.OIDDAManager.AddDirectorIntensity(amount, reason);
    }

    /// <summary>
    /// Gets a value indicating whether an encounter should be spawned based on the current pacing settings.
    /// </summary>
    /// <remarks>If pacing is enabled, this property reflects the recommendation of the pacing director. If pacing is disabled, it always returns <see langword="true"/>.</remarks>
    public bool IsShouldSpawnEncounter => (OIDDAUtils.OIDDAManager) ? OIDDAUtils.OIDDAManager.IsShouldSpawnEncounter : false;
    /// <summary>
    /// Gets the current pacing state of the director.
    /// </summary>
    public DirectorState CurrentState => (OIDDAUtils.OIDDAManager) ? OIDDAUtils.OIDDAManager.DirectorState : DirectorState.Build;
    /// <summary>
    /// Gets the current intensity level of the game loop.
    /// </summary>
    public float CurrentIntensity => (OIDDAUtils.OIDDAManager) ? OIDDAUtils.OIDDAManager.Intensity : 0.0f;
    /// <summary>
    /// Gets the current stress level of the player as determined by the Pacing Director.
    /// </summary>
    public float CurrentStress => (OIDDAUtils.OIDDAManager) ? OIDDAUtils.OIDDAManager.PlayerStress : 0.0f;
    /// <summary>
    /// Gets the current fatigue level of the player.
    /// </summary>
    public float CurrentFatigue => (OIDDAUtils.OIDDAManager) ? OIDDAUtils.OIDDAManager.PlayerFatigue : 0.0f;

    #endregion
}