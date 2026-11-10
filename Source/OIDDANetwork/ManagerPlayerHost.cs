using OIDDA.Data;
using System.Collections.Generic;

namespace OIDDA.Network;

/// <summary>
/// <see cref="IOIDDAPlayerHost"/> over the real <see cref="OIDDAManager"/>.
/// </summary>
public sealed class ManagerPlayerHost : IOIDDAPlayerHost
{
    public OIDDAManager Manager { get; }

    readonly List<OIDDAGroup> _groups = new();

    public ManagerPlayerHost(OIDDAManager manager) => Manager = manager;

    public bool RegisterPlayer(uint playerId) => Manager.RegisterPlayer(playerId) != null;

    public bool UnregisterPlayer(uint playerId) => Manager.UnregisterPlayer(playerId);

    public object ParkPlayer(uint playerId) => Manager.ParkPlayer(playerId);

    public bool ResumePlayer(uint playerId, object parked) => parked is OIDDAContext context && Manager.ResumePlayer(playerId, context) != null;

    public bool TryGetPlayerState(uint playerId, out OIDDAStateSnapshot snapshot) => Manager.TryGetPlayerState(playerId, out snapshot);

    public bool TryGetGroupState(out OIDDAStateSnapshot snapshot) => Manager.TryGetGroupState(out snapshot);

    public uint GetPlayerGroup(uint playerId) => Manager.GetContext(playerId)?.GroupId ?? OIDDAManager.DefaultGroupId;

    public void GetGroupStates(List<KeyValuePair<uint, OIDDAStateSnapshot>> into)
    {
        Manager.GetGroups(_groups);
        for (int i = 0; i < _groups.Count; i++)
            into.Add(new KeyValuePair<uint, OIDDAStateSnapshot>(_groups[i].Id, OIDDAStateSnapshot.From(_groups[i])));
    }

    public void SetDefaultPlayerIsReal(bool value) => Manager.DefaultPlayerIsReal = value;

    public byte[] CaptureProfile(uint playerId)
    {
        var context = Manager.GetContext(playerId);
        return context == null ? null : OIDDAProfile.Capture(context);
    }

    public bool RestoreProfile(uint playerId, byte[] profile)
    {
        var context = Manager.GetContext(playerId);
        return context != null && OIDDAProfile.TryRestore(context, profile);
    }

    public bool WriteMetric(uint playerId, string name, float value)
    {
        var context = Manager.GetContext(playerId);
        if (context == null) return false;

        context.QuickSender(name, value);
        return true;
    }
}
