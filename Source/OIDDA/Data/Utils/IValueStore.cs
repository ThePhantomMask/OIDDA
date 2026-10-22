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
