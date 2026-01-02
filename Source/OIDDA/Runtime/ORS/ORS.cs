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

    public abstract bool TryReceiverValue<T>(out T result);

    public abstract T ReceiverValue<T>(string nameValue);

    public abstract T ReceiverValue<T>();

    public abstract T QuickReceiver<T>(string NameValue);

    public abstract bool TrySenderValue(string nameValue, object senderValue);

    public abstract bool TrySenderValue(object senderValue);

    public abstract void SenderValue(string nameValue, object senderValue);

    public abstract void SenderValue(object senderValue);

    public abstract void QuickSender(string nameValue, object senderValue);
}

/// <summary>
///  OIDDA Receiver Sender Agent
/// </summary>
[Category(name: "OIDDA")]
public class ORS : ORSAgent
{
    string ORSID, ORSName;

    public static ORS Instance = new();

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
    public override void ConnectORSAgent(ORSUtils.ORSType type)
    {
        if (!OIDDAUtils.OIDDAManager) return;
        var Dynamic = new IORSAgentD(); Dynamic.ORSType = type;
        OIDDAUtils.OIDDAManager.Connect(ORSID = ORSUtils.GeneratedID, Dynamic);
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
    public override void DisconnectORSAgent(ORSUtils.ORSType type)
    {
        if (!OIDDAUtils.OIDDAManager) return;
        OIDDAUtils.OIDDAManager.Disconnect(ORSID, type);
    }

    public override bool TryReceiverValue<T>(string nameValue, out T result)
    {
        if (!OIDDAUtils.OIDDAManager) { result = default; return false; }
        if (IsConnected && OIDDAUtils.OIDDAManager.VerifyIsReceiver(ORSID))
        {
            result = OIDDAUtils.OIDDAManager.GetGlobal<T>(nameValue); return true;
        }
        result = default; return false;
    }

    public override bool TryReceiverValue<T>(out T result)
    {
        if (!OIDDAUtils.OIDDAManager) { result = default; return false; }
        if (IsConnected && OIDDAUtils.OIDDAManager.VerifyIsStaticReceiver(ORSName))
        {
            result = OIDDAUtils.OIDDAManager.GetStaticGlobal<T>(ORSName); return true;
        }
        result = default; return false;
    }

    public override T QuickReceiver<T>(string NameValue)
    {
        if (!OIDDAUtils.OIDDAManager) throw new InvalidOperationException("OIDDA Manager instance is not available.");
        return OIDDAUtils.OIDDAManager.QuickReceiver<T>(NameValue);
    }

    public override T ReceiverValue<T>()
    {
        if (!OIDDAUtils.OIDDAManager) throw new InvalidOperationException("OIDDA Manager instance is not available.");
        if (IsConnected && OIDDAUtils.OIDDAManager.VerifyIsStaticReceiver(ORSName))
        {
            return OIDDAUtils.OIDDAManager.GetStaticGlobal<T>(ORSName);
        }
        throw new InvalidCastException($"Value for static receiver '{ORSName}' is not of type {typeof(T).Name}");
    }

    public override T ReceiverValue<T>(string nameValue)
    {
        if (!OIDDAUtils.OIDDAManager) throw new InvalidOperationException("OIDDA Manager instance is not available.");

        if (IsConnected && OIDDAUtils.OIDDAManager.VerifyIsReceiver(ORSID))
        {
            return OIDDAUtils.OIDDAManager.GetGlobal<T>(nameValue);
        }
        throw new InvalidCastException($"Value for key '{nameValue}' is not of type {typeof(T).Name}");
    }

    public override bool TrySenderValue(string nameValue, object senderValue)
    {
        if (!OIDDAUtils.OIDDAManager) return false;
        if (IsConnected && OIDDAUtils.OIDDAManager.VerifyIsSender(ORSID))
        {
            OIDDAUtils.OIDDAManager.SetGlobal(nameValue, senderValue);
            return true;
        }
        return false;
    }

    public override bool TrySenderValue(object senderValue)
    {
        if (!OIDDAUtils.OIDDAManager) return false;
        if (IsConnected && OIDDAUtils.OIDDAManager.VerifyIsStaticSender(ORSName))
        {
            OIDDAUtils.OIDDAManager.SetStaticGlobal(ORSName, senderValue);
            return true;
        }
        return false;
    }

    public override void SenderValue(string nameValue, object senderValue)
    {
        if (!OIDDAUtils.OIDDAManager) return;
        if (IsConnected && OIDDAUtils.OIDDAManager.VerifyIsSender(ORSID))
        {
            OIDDAUtils.OIDDAManager.SetGlobal(nameValue, senderValue);
        }
    }

    public override void SenderValue(object senderValue)
    {
        if (!OIDDAUtils.OIDDAManager) return;
        if (IsConnected && OIDDAUtils.OIDDAManager.VerifyIsStaticSender(ORSName))
        {
            OIDDAUtils.OIDDAManager.SetStaticGlobal(ORSName, senderValue);
            return;
        }
    }

    public override void QuickSender(string nameValue, object senderValue)
    {
        if (!OIDDAUtils.OIDDAManager) return;
        OIDDAUtils.OIDDAManager.QuickSender(nameValue,senderValue);
    }
}
