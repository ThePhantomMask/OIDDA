namespace OIDDA.Network;

/// <summary>
/// <see cref="IOIDDAPlayerHost"/> over the real <see cref="OIDDAManager"/>.
/// </summary>
public sealed class ManagerPlayerHost : IOIDDAPlayerHost
{
    public OIDDAManager Manager { get; }

    public ManagerPlayerHost(OIDDAManager manager) => Manager = manager;

    public bool RegisterPlayer(uint playerId) => Manager.RegisterPlayer(playerId) != null;

    public bool UnregisterPlayer(uint playerId) => Manager.UnregisterPlayer(playerId);

    public bool TryGetPlayerState(uint playerId, out OIDDAStateSnapshot snapshot) => Manager.TryGetPlayerState(playerId, out snapshot);

    public bool TryGetGroupState(out OIDDAStateSnapshot snapshot) => Manager.TryGetGroupState(out snapshot);

    public void SetDefaultPlayerIsReal(bool value) => Manager.DefaultPlayerIsReal = value;

    public bool WriteMetric(uint playerId, string name, float value)
    {
        var context = Manager.GetContext(playerId);
        if (context == null) return false;

        context.QuickSender(name, value);
        return true;
    }
}
