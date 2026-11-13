using FlaxEngine;
using OIDDA.Data;
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
    #region Network state (clients)

    public abstract bool TryGetPlayerState(uint playerId, out OIDDAStateSnapshot snapshot);

    public abstract bool TryGetGroupState(out OIDDAStateSnapshot snapshot);

    public abstract bool TryGetGroupState(uint groupId, out OIDDAStateSnapshot snapshot);

    #endregion

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

    #region Player API methods (multiplayer)

    public abstract T QuickReceiver<T>(uint playerId, string nameValue);

    public abstract void QuickSender(uint playerId, string nameValue, object senderValue);

    public abstract bool TryReceiverValue<T>(uint playerId, string nameValue, out T result);

    public abstract bool TryReceiverValue<T>(uint playerId, out T result);

    public abstract T ReceiverValue<T>(uint playerId, string nameValue);

    public abstract T ReceiverValue<T>(uint playerId);

    public abstract bool TrySenderValue(uint playerId, string nameValue, object senderValue);

    public abstract bool TrySenderValue(uint playerId, object senderValue);

    public abstract void SenderValue(uint playerId, string nameValue, object senderValue);

    public abstract void SenderValue(uint playerId, object senderValue);

    public abstract void AddDirectorIntensity(uint playerId, float amount, string reason = "");

    #endregion

    #region Group API methods (multiplayer)

    public abstract void AddGroupIntensity(float amount, string reason = "");

    public abstract T GroupQuickReceiver<T>(string nameValue);

    public abstract void GroupQuickSender(string nameValue, object senderValue);

    public abstract void SetGroupExcluded(uint playerId, bool excluded);

    public abstract bool SetPlayerGroup(uint playerId, uint groupId);

    public abstract void AddGroupIntensity(uint groupId, float amount, string reason = "");

    public abstract T GroupQuickReceiver<T>(uint groupId, string nameValue);

    public abstract void GroupQuickSender(uint groupId, string nameValue, object senderValue);

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

    private static OIDDAManager _oiddaManager => OIDDAPlugin.Instance?.Manager;

    #region Network state (clients)

    /// <summary>
    /// Set by a network layer (see <see cref="IOIDDAStateProvider"/>). When it reports no authority, this process is a
    /// remote client: the manager stays idle, state reads come from the provider, and changes are refused.
    /// </summary>
    public static IOIDDAStateProvider StateProvider { get; set; }

    /// <summary> True on a remote client: OIDDA does not run here, the server's state is only read. </summary>
    public static bool IsRemoteClient => StateProvider != null && !StateProvider.IsAuthority;

    /// <summary>
    /// The player this process plays as: the provider's local player on a client, the default player everywhere else.
    /// </summary>
    public uint LocalPlayerId => IsRemoteClient ? StateProvider.LocalPlayerId : OIDDAManager.DefaultPlayerId;

    /// <summary>
    /// The state of a player: from the manager where OIDDA runs, from the state provider on a remote client.
    /// False if it's not available.
    /// </summary>
    public override bool TryGetPlayerState(uint playerId, out OIDDAStateSnapshot snapshot)
    {
        if (IsRemoteClient) return StateProvider.TryGetPlayerState(playerId, out snapshot);

        var manager = _oiddaManager;
        if (!manager)
        {
            snapshot = default;
            return false;
        }
        return manager.TryGetPlayerState(playerId, out snapshot);
    }

    /// <summary>
    /// The state of the local player's group: from the manager where OIDDA runs, from the state provider on a remote client
    /// (which replicates the default group). False if there is no group or nothing was received yet.
    /// </summary>
    public override bool TryGetGroupState(out OIDDAStateSnapshot snapshot)
    {
        if (IsRemoteClient) return StateProvider.TryGetGroupState(out snapshot);

        var manager = _oiddaManager;
        if (!manager)
        {
            snapshot = default;
            return false;
        }
        return manager.TryGetGroupState(manager.DefaultContext.GroupId, out snapshot);
    }

    /// <summary>
    /// The state of a given group: from the manager where OIDDA runs, from the state provider on a remote client (which knows the
    /// groups the server sent). False if the group does not exist or is not available here.
    /// </summary>
    public override bool TryGetGroupState(uint groupId, out OIDDAStateSnapshot snapshot)
    {
        if (IsRemoteClient) return StateProvider.TryGetGroupState(groupId, out snapshot);

        var manager = _oiddaManager;
        if (!manager)
        {
            snapshot = default;
            return false;
        }
        return manager.TryGetGroupState(groupId, out snapshot);
    }

    // The local player's state as seen from a remote client.
    private static bool TryLocalRemoteState(out OIDDAStateSnapshot snapshot)
        => StateProvider.TryGetPlayerState(StateProvider.LocalPlayerId, out snapshot);

    private static readonly HashSet<string> _warned = new();

    // Warn once per kind of call, so a per-frame call on a client does not flood the log.
    private static void WarnRemote(string what)
    {
        if (_warned.Add(what))
            Debug.LogWarning($"[OIDDA] {what} is ignored on a remote client: OIDDA runs on the server.");
    }

    private static InvalidOperationException RemoteVariableError(string name)
        => new($"'{name}' is a server-side variable and cannot be read on a remote client. Read the replicated state instead (CurrentState, CurrentIntensity, GroupState, TryGetPlayerState...).");

    // A metric measured on the client: sent to the server if the provider allows it, otherwise ignored.
    private static bool ForwardMetric(string name, object value)
    {
        if (StateProvider.TryReportMetric(name, value)) return true;

        WarnRemote("Sending a value");
        return false;
    }

    private static bool ForwardMetricForPlayer(uint playerId, string name, object value)
    {
        if (playerId == StateProvider.LocalPlayerId) return ForwardMetric(name, value);

        WarnRemote("Sending a value for another player");
        return false;
    }

    #endregion

    #region DDA API methods

    private string ORSID, ORSName;
    private bool CanReceiveDynamic() => IsDynamicConnected && _oiddaManager.VerifyIsReceiver(ORSID);
    private bool CanSendDynamic() => IsDynamicConnected && _oiddaManager.VerifyIsSender(ORSID);
    private bool CanReceiveStatic() => IsStaticConnected && _oiddaManager.VerifyIsStaticReceiver(ORSName);
    private bool CanSendStatic() => IsStaticConnected && _oiddaManager.VerifyIsStaticSender(ORSName);

    private bool IsDynamicConnected => _oiddaManager && !string.IsNullOrEmpty(ORSID) && _oiddaManager.ORSIsConnected(ORSID);
    private bool IsStaticConnected => _oiddaManager && !string.IsNullOrEmpty(ORSName) && _oiddaManager.StaticORSIsConnected(ORSName);

    public bool IsConnected => IsDynamicConnected || IsStaticConnected;

    /// <summary>
    /// Initializes the ORS agent connection using the specified script and agent type (Static ORS Agent).
    /// </summary>
    /// <param name="AgentName">The script instance that defines the connection parameters and logic for the ORS agent.</param>
    public override void ConnectORSAgent(string AgentName)
    {
        if (!_oiddaManager) return;
        _oiddaManager.Connect(ORSName = AgentName);
    }

    /// <summary>
    /// Initializes the ORS agent connection using the specified ORS type (Dynamic ORS Agent).
    /// </summary>
    /// <param name="type">The ORS type to use for the agent connection. Determines the configuration and behavior of the agent.</param>
    public override void ConnectORSAgent(ORSType type)
    {
        if (!_oiddaManager) return;

        // Reconnecting must not leave the previous dynamic agent registered.
        if (!string.IsNullOrEmpty(ORSID)) _oiddaManager.Disconnect(ORSID, type);

        _oiddaManager.Connect(ORSID = ORSUtils.GeneratedID, new IORSAgentD
        {
            ORSType = type
        });
    }

    /// <summary>
    ///  Disconnects the specified ORS agent represented by the provided script (Static ORS Agent).
    /// </summary>
    public override void DisconnectORSAgent()
    {
        if (!_oiddaManager) return;
        _oiddaManager.Disconnect(ORSName);
    }

    /// <summary>
    /// Disconnects the ORS agent from the current session (Dynamic ORS Agent).
    /// </summary>
    /// /// <param name="type">The type of ORS agent to connect to. Specifies the agent category or behavior.</param>
    public override void DisconnectORSAgent(ORSType type)
    {
        if (!_oiddaManager) return;
        _oiddaManager.Disconnect(ORSID, type);
    }

    public override bool TryReceiverValue<T>(string nameValue, out T result)
    {
        if (IsRemoteClient)
        {
            result = default; return false;
        }
        if (!(_oiddaManager || CanReceiveDynamic())) 
        {
            result = default; return false; 
        }
        result = _oiddaManager.GetGlobal<T>(nameValue); return true;
    }

    public override bool TryReceiverValue<T>(out T result)
    {
        if (IsRemoteClient)
        {
            result = default; return false;
        }
        if (!(_oiddaManager || CanReceiveStatic())) 
        {
            result = default; return false; 
        }
        result = _oiddaManager.GetStaticGlobal<T>(ORSName); return true;
    }

    public override T QuickReceiver<T>(string NameValue)
    {
        if (IsRemoteClient) throw RemoteVariableError(NameValue);
        return _oiddaManager ? _oiddaManager.QuickReceiver<T>(NameValue) : default(T);
    }

    public override T ReceiverValue<T>()
    {
        if (IsRemoteClient) throw RemoteVariableError(ORSName);
        TryReceiverValue(out T result);
        return result;
    }

    public override T ReceiverValue<T>(string nameValue)
    {
        if (IsRemoteClient) throw RemoteVariableError(nameValue);
        TryReceiverValue(nameValue, out T result);
        return result;
    }

    public override bool TrySenderValue(string nameValue, object senderValue)
    {
        if (IsRemoteClient) return ForwardMetric(nameValue, senderValue);
        if (!(_oiddaManager || CanSendDynamic())) return false;
        _oiddaManager.SetGlobal(nameValue, senderValue);
        return true;
    }

    public override bool TrySenderValue(object senderValue)
    {
        if (IsRemoteClient) return ForwardMetric(_oiddaManager.StaticVariable(ORSName), senderValue);
        if (!(_oiddaManager || CanSendStatic())) return false;
        _oiddaManager.SetStaticGlobal(ORSName, senderValue);
        return true;
    }

    public override void SenderValue(string nameValue, object senderValue) => TrySenderValue(nameValue, senderValue);

    public override void SenderValue(object senderValue) => TrySenderValue(senderValue);

    /// <summary>
    /// Writes a value straight into the gameplay globals without triggering a new OIDDA analysis.
    /// Used by rule application and smoothing, which run inside an analysis cycle.
    /// </summary>
    internal void WriteValue(string nameValue, object value)
    {
        var manager = _oiddaManager;
        if (!manager) return;
        manager.WriteGlobal(nameValue, value);
    }

    public override void QuickSender(string nameValue, object senderValue)
    {
        if (IsRemoteClient)
        {
            ForwardMetric(nameValue, senderValue);
            return;
        }
        if (!_oiddaManager) return;
        _oiddaManager.QuickSender(nameValue, senderValue);
    }

    #endregion

    #region Player API methods (multiplayer)

    // The overloads below act on one player's context instead of the default one. The player id comes first so that
    // a call like SenderValue("health", 5u) can never be mistaken for SenderValue(object, playerId).
    // Connection state (which agents exist, and whether they send or receive) is shared by every player.

    /// <summary>
    /// The context of a registered player (see <see cref="OIDDAManager.RegisterPlayer"/>), or null.
    /// </summary>
    public OIDDAContext GetPlayerContext(uint playerId) => !IsRemoteClient && _oiddaManager ? _oiddaManager.GetContext(playerId) : null;

    private static OIDDAContext RequirePlayer(uint playerId)
    {
        if (IsRemoteClient) throw new InvalidOperationException("Player contexts only exist where OIDDA runs, not on a remote client. Use TryGetPlayerState to read a player's state.");

        var manager = _oiddaManager;
        if (!manager) throw new InvalidOperationException("OIDDA Manager instance is not available.");

        return manager.GetContext(playerId) ?? throw new InvalidOperationException($"Player {playerId} is not registered.");
    }

    public override T QuickReceiver<T>(uint playerId, string nameValue) => RequirePlayer(playerId).QuickReceiver<T>(nameValue);

    public override void QuickSender(uint playerId, string nameValue, object senderValue)
    {
        if (IsRemoteClient)
        {
            ForwardMetricForPlayer(playerId, nameValue, senderValue);
            return;
        }
        GetPlayerContext(playerId)?.QuickSender(nameValue, senderValue);
    }

    public override bool TryReceiverValue<T>(uint playerId, string nameValue, out T result)
    {
        var context = GetPlayerContext(playerId);
        if (context == null || !CanReceiveDynamic())
        {
            result = default; return false;
        }
        result = context.GetGlobal<T>(nameValue); return true;
    }

    public override bool TryReceiverValue<T>(uint playerId, out T result)
    {
        var context = GetPlayerContext(playerId);
        if (context == null || !CanReceiveStatic())
        {
            result = default; return false;
        }
        result = context.GetGlobal<T>(_oiddaManager.StaticVariable(ORSName)); return true;
    }

    public override T ReceiverValue<T>(uint playerId, string nameValue)
    {
        if (!TryReceiverValue(playerId, nameValue, out T result)) throw new InvalidOperationException($"OIDDA Manager instance is not available, player {playerId} is not registered, or ORS is not connected.");
        return result;
    }

    public override T ReceiverValue<T>(uint playerId)
    {
        if (!TryReceiverValue(playerId, out T result)) throw new InvalidOperationException($"OIDDA Manager instance is not available, player {playerId} is not registered, or ORS is not connected.");
        return result;
    }

    public override bool TrySenderValue(uint playerId, string nameValue, object senderValue)
    {
        if (IsRemoteClient) return CanSendDynamic() && ForwardMetricForPlayer(playerId, nameValue, senderValue);

        var context = GetPlayerContext(playerId);
        if (context == null || !CanSendDynamic()) return false;
        context.SetGlobal(nameValue, senderValue);
        return true;
    }

    public override bool TrySenderValue(uint playerId, object senderValue)
    {
        if (IsRemoteClient) return CanSendStatic() && ForwardMetricForPlayer(playerId, _oiddaManager.StaticVariable(ORSName), senderValue);

        var context = GetPlayerContext(playerId);
        if (context == null || !CanSendStatic()) return false;
        context.SetStaticGlobal(_oiddaManager.StaticVariable(ORSName), senderValue);
        return true;
    }

    public override void SenderValue(uint playerId, string nameValue, object senderValue) => TrySenderValue(playerId, nameValue, senderValue);

    public override void SenderValue(uint playerId, object senderValue) => TrySenderValue(playerId, senderValue);

    /// <summary>
    /// Adds pacing intensity to one player's personal Director.
    /// </summary>
    public override void AddDirectorIntensity(uint playerId, float amount, string reason = "")
    {
        if (IsRemoteClient)
        {
            WarnRemote("AddDirectorIntensity");
            return;
        }
        GetPlayerContext(playerId)?.AddDirectorIntensity(amount, reason);
    }

    #endregion

    #region Group API methods (multiplayer)

    // The group is enabled by assigning an OIDDAGroupConfig to the manager. Without it these members do nothing
    // and report neutral values. Everyone starts in group 0; SetPlayerGroup moves a player to another group. The members without a
    // group id (Group, GroupState, GroupIntensity, GroupQuickSender...) act on the group of the local player (the default player,
    // which is group 0 unless it was moved); the ones with a group id act on that group.

    /// <summary> The group of the local player (group 0 by default), or null when no group is configured. </summary>
    public OIDDAGroup Group
    {
        get
        {
            if (IsRemoteClient) return null;

            var manager = _oiddaManager;
            return manager ? manager.GetGroup(manager.DefaultContext.GroupId) : null;
        }
    }

    /// <summary> The group with the given id, or null if it does not exist, no group is configured, or on a remote client. </summary>
    public OIDDAGroup GetGroup(uint groupId) => !IsRemoteClient && _oiddaManager ? _oiddaManager.GetGroup(groupId) : null;

    /// <summary>
    /// Moves a player to a group, creating the group if needed. Everyone starts in group 0. See <see cref="OIDDAManager.SetPlayerGroup"/>.
    /// </summary>
    /// <returns>False if the player is not registered, MaxGroups is reached, or on a remote client (with a warning).</returns>
    public override bool SetPlayerGroup(uint playerId, uint groupId)
    {
        if (IsRemoteClient)
        {
            WarnRemote("SetPlayerGroup");
            return false;
        }

        var manager = _oiddaManager;
        return manager && manager.SetPlayerGroup(playerId, groupId);
    }

    /// <summary>
    /// Adds intensity that belongs to the whole group (for example a boss that threatens everyone).
    /// </summary>
    public override void AddGroupIntensity(float amount, string reason = "")
    {
        if (IsRemoteClient)
        {
            WarnRemote("AddGroupIntensity");
            return;
        }
        Group?.AddIntensity(amount, reason);
    }

    /// <summary>
    /// Adds intensity that belongs to one group. Ignored if the group does not exist.
    /// </summary>
    public override void AddGroupIntensity(uint groupId, float amount, string reason = "")
    {
        if (IsRemoteClient)
        {
            WarnRemote("AddGroupIntensity");
            return;
        }
        GetGroup(groupId)?.AddIntensity(amount, reason);
    }

    /// <summary>
    /// Leaves one player out of the group (or takes them back in): an excluded player does not count in the group's aggregation,
    /// but keeps its own difficulty and still reads the Group.* variables. Typical use: exclude a player when they die, include
    /// them again when they come back. Ignored for a player that is not registered, and on a remote client (with a warning).
    /// </summary>
    public override void SetGroupExcluded(uint playerId, bool excluded)
    {
        if (IsRemoteClient)
        {
            WarnRemote("SetGroupExcluded");
            return;
        }

        var context = GetPlayerContext(playerId);
        if (context) context.ExcludedFromGroup = excluded;
    }

    /// <summary> 
    /// True when no group is configured (or its state is not available), so spawning is never blocked by a missing group. 
    /// </summary>
    public bool IsShouldSpawnGroupEncounter
    {
        get
        {
            if (IsRemoteClient) return !StateProvider.TryGetGroupState(out var remote) || remote.ShouldSpawnEncounter;
            return Group?.ShouldSpawnEncounter ?? true;
        }
    }

    /// <summary>
    /// Gets a value indicating if the group of the local player is in a flow state. False when no group is configured.
    /// On a remote client it needs state format V3 (with V2 it is always false).
    /// </summary>
    public bool IsInGroupFlowState
    {
        get
        {
            if (IsRemoteClient) return StateProvider.TryGetGroupState(out var remote) && remote.IsInFlowState;
            return Group?.IsInFlowState ?? false;
        }
    }

    public DirectorState GroupState
    {
        get
        {
            if (IsRemoteClient) return StateProvider.TryGetGroupState(out var remote) ? remote.State : DirectorState.Build;
            return Group != null ? Group.State : DirectorState.Build;
        }
    }

    public float GroupIntensity => IsRemoteClient ? (StateProvider.TryGetGroupState(out var remote) ? remote.Intensity : 0f) : Group?.Intensity ?? 0f;

    public float GroupStress => IsRemoteClient ? (StateProvider.TryGetGroupState(out var remote) ? remote.Stress : 0f) : Group?.Stress ?? 0f;

    public float GroupFatigue => IsRemoteClient ? (StateProvider.TryGetGroupState(out var remote) ? remote.Fatigue : 0f) : Group?.Fatigue ?? 0f;

    /// <summary>
    /// Reads a group-level variable (including the read-only Group.* ones).
    /// </summary>
    public override T GroupQuickReceiver<T>(string nameValue)
    {
        if (IsRemoteClient) throw RemoteVariableError(nameValue);

        var group = Group ?? throw new InvalidOperationException("No group is configured.");
        return group.GetValue<T>(nameValue);
    }

    /// <summary>
    /// Reads a variable of one group (including the read-only Group.* ones).
    /// </summary>
    public override T GroupQuickReceiver<T>(uint groupId, string nameValue)
    {
        if (IsRemoteClient) throw RemoteVariableError(nameValue);

        var group = GetGroup(groupId) ?? throw new InvalidOperationException($"Group {groupId} does not exist (or no group is configured).");
        return group.GetValue<T>(nameValue);
    }

    /// <summary>
    /// Writes a group-level variable. Ignored when no group is configured.
    /// </summary>
    public override void GroupQuickSender(string nameValue, object senderValue)
    {
        if (IsRemoteClient)
        {
            WarnRemote("GroupQuickSender");
            return;
        }
        Group?.SetValue(nameValue, senderValue);
    }

    /// <summary>
    /// Writes a variable of one group. Ignored if the group does not exist.
    /// </summary>
    public override void GroupQuickSender(uint groupId, string nameValue, object senderValue)
    {
        if (IsRemoteClient)
        {
            WarnRemote("GroupQuickSender");
            return;
        }
        GetGroup(groupId)?.SetValue(nameValue, senderValue);
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
        if (IsRemoteClient)
        {
            WarnRemote("AddDirectorIntensity");
            return;
        }
        if (!_oiddaManager) return;
        _oiddaManager.AddDirectorIntensity(amount, reason);
    }

    /// <summary>
    /// Gets a value indicating whether an encounter should be spawned based on the current pacing settings.
    /// </summary>
    /// <remarks>If pacing is enabled, this property reflects the recommendation of the pacing director. If pacing is disabled, it always returns <see langword="true"/>.</remarks>
    public bool IsShouldSpawnEncounter => IsRemoteClient ? TryLocalRemoteState(out var remote) && remote.ShouldSpawnEncounter : (_oiddaManager) ? _oiddaManager.IsShouldSpawnEncounter : false;
    /// <summary>
    /// Gets a value indicating if the player is in a flow state: engaged, without too much stress or fatigue.
    /// </summary>
    /// <remarks>False if pacing is disabled. For another player, use <c>TryGetPlayerState</c> (the snapshot carries the same flag).</remarks>
    public bool IsInFlowState => IsRemoteClient ? TryLocalRemoteState(out var remote) && remote.IsInFlowState : (_oiddaManager) ? _oiddaManager.IsInFlowState : false;
    /// <summary>
    /// Gets the current pacing state of the director.
    /// </summary>
    public DirectorState CurrentState => IsRemoteClient ? (TryLocalRemoteState(out var remote) ? remote.State : DirectorState.Build) : (_oiddaManager) ? _oiddaManager.DirectorState : DirectorState.Build;
    /// <summary>
    /// Gets the current intensity level of the game loop.
    /// </summary>
    public float CurrentIntensity => IsRemoteClient ? (TryLocalRemoteState(out var remote) ? remote.Intensity : 0f) : (_oiddaManager) ? _oiddaManager.Intensity : 0.0f;
    /// <summary>
    /// Gets the current stress level of the player as determined by the Pacing Director.
    /// </summary>
    public float CurrentStress => IsRemoteClient ? (TryLocalRemoteState(out var remote) ? remote.Stress : 0f) : (_oiddaManager) ? _oiddaManager.PlayerStress : 0.0f;
    /// <summary>
    /// Gets the current fatigue level of the player.
    /// </summary>
    public float CurrentFatigue => IsRemoteClient ? (TryLocalRemoteState(out var remote) ? remote.Fatigue : 0f) : (_oiddaManager) ? _oiddaManager.PlayerFatigue : 0.0f;

    #endregion
}