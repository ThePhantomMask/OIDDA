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
    public abstract void ConnectORSAgent(Script script, ORSUtils.ORSType type);

    public abstract void ConnectORSAgent(ORSUtils.ORSType type);

    public abstract void DisconnectORSAgent(Script script);

    public abstract void DisconnectORSAgent();

    public abstract bool TryReceiverValue<T>(string nameValue, out T result);

    public abstract T ReceiverValue<T>(string nameValue);

    public abstract void SenderValue(string nameValue, object senderValue);

    public abstract bool TrySenderValue(string nameValue, object senderValue);
}

/// <summary>
///  Agent
/// </summary>
public class ORS : ORSAgent
{
    string ORSID;

    public static ORS Instance = new ORS();

    OIDDAManager OIDDAManager => Level.FindActor<OIDDAManager>();

    public bool IsStaticConnected;

    public bool IsDynamicConnected;

    /// <summary>
    /// Initializes the ORS agent connection using the specified script and agent type (Static ORS Agent).
    /// </summary>
    /// <param name="script">The script instance that defines the connection parameters and logic for the ORS agent.</param>
    /// <param name="type">The type of ORS agent to connect to. Specifies the agent category or behavior.</param>
    public override void ConnectORSAgent(Script script, ORSUtils.ORSType type)
    {

    }

    /// <summary>
    /// Initializes the ORS agent connection using the specified ORS type (Dynamic ORS Agent).
    /// </summary>
    /// <param name="type">The ORS type to use for the agent connection. Determines the configuration and behavior of the agent.</param>
    public override void ConnectORSAgent(ORSUtils.ORSType type)
    {
        ORSID = ORSUtils.GeneratedID;
    }

    /// <summary>
    /// Disconnects the specified ORS agent represented by the provided script (Static ORS Agent).
    /// </summary>
    /// <param name="script">The script that identifies the ORS agent to disconnect. Cannot be null.</param>
    public override void DisconnectORSAgent(Script script = null)
    {
        if (script)
        {

        }
    }

    /// <summary>
    /// Disconnects the ORS agent from the current session (Dynamic ORS Agent).
    /// </summary>
    public override void DisconnectORSAgent()
    {

    }

    public override bool TryReceiverValue<T>(string nameValue, out T result)
    {
        if (IsDynamicConnected || IsStaticConnected)
        {
            result = default; return true;
        }

        result = default; return false;
    }

    public override T ReceiverValue<T>(string nameValue)
    {
        if (IsDynamicConnected || IsStaticConnected)
        {
            throw new InvalidCastException($"Value for key '{nameValue}' is not of type {typeof(T).Name}");
        }

        throw new InvalidCastException($"Value for key '{nameValue}' is not of type {typeof(T).Name}");
    }

    public override void SenderValue(string nameValue, object senderValue)
    {
        if (IsDynamicConnected || IsStaticConnected)
        {

        }


    }

    public override bool TrySenderValue(string nameValue, object senderValue)
    {
        if (IsDynamicConnected || IsStaticConnected)
        {
            return true;
        }

        return false;
    }

}
