using FlaxEngine;
using System.Collections.Generic;

namespace OIDDA;

/// <summary>
/// Where the gameplay values of one OIDDA context live. The single-player store is backed by a
/// <see cref="GameplayGlobals"/> asset; other implementations can keep the values elsewhere (for example one set per player).
/// </summary>
public interface IValueStore
{
    /// <summary> False when the underlying storage is missing or destroyed. </summary>
    bool IsValid { get; }

    /// <summary> Snapshot of every value, keyed by variable name. </summary>
    Dictionary<string, object> Values { get; }

    T GetValue<T>(string name);

    void SetValue(string name, object value);

    /// <summary> Restores every value to its initial state. </summary>
    void ResetValues();
}

/// <summary>
/// <see cref="IValueStore"/> that keeps its values in a plain dictionary. Used for every player except the default one,
/// because a <see cref="GameplayGlobals"/> asset only has one set of values for the whole game.
/// </summary>
public sealed class DictionaryValueStore : IValueStore
{
    readonly Dictionary<string, object> _values = new();
    readonly Dictionary<string, object> _initial = new();

    public DictionaryValueStore(IEnumerable<KeyValuePair<string, object>> initialValues = null)
    {
        if (initialValues == null) return;

        foreach (var kv in initialValues)
        {
            _values[kv.Key] = kv.Value;
            _initial[kv.Key] = kv.Value;
        }
    }

    public bool IsValid => true;

    public Dictionary<string, object> Values => _values;

    /// <summary> Same contract as GameplayGlobals: a missing name or a different type gives the default value. </summary>
    public T GetValue<T>(string name) => _values.TryGetValue(name, out var value) && value is T typed ? typed : default;

    public void SetValue(string name, object value) => _values[name] = value;

    public void ResetValues()
    {
        _values.Clear();
        foreach (var kv in _initial) _values[kv.Key] = kv.Value;
    }

    /// <summary>
    /// Adds the variables that are not present yet (and makes them part of the initial state). Existing values are kept.
    /// </summary>
    public void SeedMissing(IEnumerable<KeyValuePair<string, object>> defaults)
    {
        if (defaults == null) return;

        foreach (var kv in defaults)
        {
            if (_values.ContainsKey(kv.Key)) continue;
            _values[kv.Key] = kv.Value;
            _initial[kv.Key] = kv.Value;
        }
    }
}

/// <summary>
/// <see cref="IValueStore"/> backed by a <see cref="GameplayGlobals"/> asset (the behaviour OIDDA always had).
/// </summary>
public sealed class GameplayGlobalsStore : IValueStore
{
    public GameplayGlobals Globals { get; }

    public GameplayGlobalsStore(GameplayGlobals globals) => Globals = globals;

    public bool IsValid => Globals;

    public Dictionary<string, object> Values => Globals.Values;

    public T GetValue<T>(string name) => Globals.GetValue<T>(name);

    public void SetValue(string name, object value) => Globals.SetValue(name, value);

    public void ResetValues() => Globals.ResetValues();
}
