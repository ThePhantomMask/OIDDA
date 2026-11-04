using System.Collections.Generic;

namespace OIDDA;

/// <summary>
/// What a player's metrics and rules read when a group is active: the player's own values plus the read-only Group.* variables.
/// Writes always go to the player's own store.
/// </summary>
internal sealed class GroupOverlayStore : IValueStore
{
    readonly OIDDAContext _owner;
    readonly OIDDAGroup _group;
    readonly Dictionary<string, object> _merged = new();

    public GroupOverlayStore(OIDDAContext owner, OIDDAGroup group)
    {
        _owner = owner;
        _group = group;
    }

    IValueStore Inner => _owner.Store;

    public bool IsValid => Inner != null && Inner.IsValid;

    // Rebuilt on every access: it is read once per analysis cycle, and the inner values can change at any time.
    public Dictionary<string, object> Values
    {
        get
        {
            _merged.Clear();
            foreach (var kv in Inner.Values) _merged[kv.Key] = kv.Value;
            foreach (var kv in _group.Variables) _merged[kv.Key] = kv.Value;
            return _merged;
        }
    }

    public T GetValue<T>(string name)
    {
        if (_group.Variables.TryGetValue(name, out var groupValue)) return groupValue is T typed ? typed : default;
        return Inner.GetValue<T>(name);
    }

    public void SetValue(string name, object value) => Inner.SetValue(name, value);

    public void ResetValues() => Inner.ResetValues();
}
