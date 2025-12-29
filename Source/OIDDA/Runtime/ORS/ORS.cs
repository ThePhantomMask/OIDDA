using FlaxEngine;
using System;
using System.Collections.Generic;

namespace OIDDA;

/// <summary>
/// Represents an abstract base class for an ORS (OIDDA Receiver Sender) agent, providing methods for connecting,
/// disconnecting, and exchanging values with an ORS system.
/// </summary>
/// <remarks>This class defines the core contract for interacting with ORS agents, including connection management
/// and value transmission. Derived classes should implement the abstract connection methods to provide specific ORS
/// agent behaviors. Thread safety and connection state management are the responsibility of the implementing
/// class.</remarks>
public abstract class ORSAgent
{
    public abstract void ConnectORSAgent(string AgentName);

    public abstract void ConnectORSAgent(ORSUtils.ORSType type);

    public abstract void DisconnectORSAgent();

    public abstract void DisconnectORSAgent(ORSUtils.ORSType type);

    public abstract bool TryReceiverValue<T>(string nameValue, out T result);

    public abstract T ReceiverValue<T>(string nameValue);

    public abstract void SenderValue(string nameValue, object senderValue);

    public abstract bool TrySenderValue(string nameValue, object senderValue);
}

/// <summary>
///  OIDDA Receiver Sender Agent
/// </summary>
public class ORS : ORSAgent
{
    string ORSID, ORSName;

    public static ORS Instance = new();

    OIDDAManager OIDDAManager => Level.FindScript<OIDDAManager>();

    public bool IsConnected => OIDDAManager.ORSIsConnected(ORSID) || OIDDAManager.ORSIsConnected();

    /// <summary>
    /// Initializes the ORS agent connection using the specified script and agent type (Static ORS Agent).
    /// </summary>
    /// <param name="AgentName">The script instance that defines the connection parameters and logic for the ORS agent.</param>
    public override void ConnectORSAgent(string AgentName)
    {
        if (!OIDDAManager) return;
        OIDDAManager.Connect(ORSName = AgentName);
    }

    /// <summary>
    /// Initializes the ORS agent connection using the specified ORS type (Dynamic ORS Agent).
    /// </summary>
    /// <param name="type">The ORS type to use for the agent connection. Determines the configuration and behavior of the agent.</param>
    public override void ConnectORSAgent(ORSUtils.ORSType type)
    {
        if (!OIDDAManager) return;
        var Dynamic = new IORSAgentD(); Dynamic.ORSType = type;
        OIDDAManager.Connect(ORSID = ORSUtils.GeneratedID, Dynamic);
    }

    /// <summary>
    ///  Disconnects the specified ORS agent represented by the provided script (Static ORS Agent).
    /// </summary>
    public override void DisconnectORSAgent()
    {
        if (!OIDDAManager) return;
        Debug.Log(ORSName);
        OIDDAManager.Disconnect(ORSName);
    }

    /// <summary>
    /// Disconnects the ORS agent from the current session (Dynamic ORS Agent).
    /// </summary>
    /// /// <param name="type">The type of ORS agent to connect to. Specifies the agent category or behavior.</param>
    public override void DisconnectORSAgent(ORSUtils.ORSType type)
    {
        if (!OIDDAManager) return;
        OIDDAManager.Disconnect(ORSID, type);
    }

    public override bool TryReceiverValue<T>(string nameValue, out T result)
    {
        if (!OIDDAManager) { result = default; return false; }
        if (IsConnected && OIDDAManager.VerifyIsStaticReceiver(ORSName) && string.IsNullOrEmpty(nameValue))
        {
            result = OIDDAManager.GetStaticGlobal<T>(ORSName); return true;
        }

        if (IsConnected && OIDDAManager.VerifyIsReceiver(ORSID))
        {
            result = OIDDAManager.GetGlobal<T>(nameValue); return true;
        }
        result = default; return false;
    }

    public override T ReceiverValue<T>(string nameValue)
    {
        if (!OIDDAManager) throw new InvalidOperationException("OIDDA Manager instance is not available.");
        if (IsConnected && OIDDAManager.VerifyIsStaticReceiver(ORSName) && string.IsNullOrEmpty(nameValue))
        {
            return OIDDAManager.GetStaticGlobal<T>(ORSName);
        }

        if (IsConnected && OIDDAManager.VerifyIsReceiver(ORSID))
        {
            return OIDDAManager.GetGlobal<T>(nameValue);
        }
        throw new InvalidCastException($"Value for key '{nameValue}' is not of type {typeof(T).Name}");
    }

    public override void SenderValue(string nameValue, object senderValue)
    {
        if (!OIDDAManager) return;
        if (IsConnected && OIDDAManager.VerifyIsStaticSender(ORSName) && string.IsNullOrEmpty(nameValue))
        {
            OIDDAManager.SetStaticGlobal(ORSName, senderValue);
            return;
        }

        if (IsConnected && OIDDAManager.VerifyIsSender(ORSID))
        {
            OIDDAManager.SetGlobal(nameValue, senderValue);
        }
    }

    public override bool TrySenderValue(string nameValue, object senderValue)
    {
        if (!OIDDAManager) return false;
        if (IsConnected && OIDDAManager.VerifyIsStaticSender(ORSName) && string.IsNullOrEmpty(nameValue))
        {
            OIDDAManager.SetStaticGlobal(ORSName, senderValue);
            return true;
        }

        if (IsConnected && OIDDAManager.VerifyIsSender(ORSID))
        {
            OIDDAManager.SetGlobal(nameValue, senderValue);
            return true;
        }
        return false;
    }
}
