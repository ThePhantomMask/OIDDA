using FlaxEngine;
using OIDDA.Data;
using System.Collections.Generic;

namespace OIDDA;

/// <summary>
/// Values the group Director takes from the players.
/// </summary>
public readonly struct GroupInputs
{
    public readonly float Score, Intensity, Stress, Fatigue;

    public GroupInputs(float score, float intensity, float stress, float fatigue)
    {
        Score = score;
        Intensity = intensity;
        Stress = stress;
        Fatigue = fatigue;
    }
}

/// <summary>
/// Names of the read-only variables the group publishes. Metrics and rule conditions of every player can use them like any
/// other variable (for example "Group.Intensity" greater than 50).
/// </summary>
public static class OIDDAGroupVariables
{
    /// <summary> Aggregated difficulty score of the players (0 easy .. 1 difficult). </summary>
    public const string Score = "Group.Score";
    /// <summary> Group intensity (aggregated players' intensity plus group boosts). </summary>
    public const string Intensity = "Group.Intensity";
    public const string Stress = "Group.Stress";
    public const string Fatigue = "Group.Fatigue";
    /// <summary> Group pacing state as an integer: 0 Build, 1 Peak, 2 Fade, 3 Relax. </summary>
    public const string State = "Group.State";
    /// <summary> Difficulty multiplier suggested by the group pacing. </summary>
    public const string Multiplier = "Group.Multiplier";
    /// <summary> Number of players taken into account. </summary>
    public const string PlayerCount = "Group.PlayerCount";
}

/// <summary>
/// The group (party) level: a Director that paces the whole session from the aggregated state of every player, and a set of
/// group-level variables that group rules act on. Enabled by assigning a <see cref="OIDDAGroupConfig"/> to the manager.
/// </summary>
public sealed class OIDDAGroup
{
    readonly OIDDAManager _owner;
    readonly DirectorManager _director;
    readonly DictionaryValueStore _store = new();
    readonly Dictionary<string, object> _variables = new();
    readonly List<float> _scores = new(), _intensities = new(), _stresses = new(), _fatigues = new(), _weights = new(), _scratch = new();

    OIDDAGroupConfig _config;
    float _accumulator;

    internal OIDDAGroup(OIDDAManager owner, DirectorManager director)
    {
        _owner = owner;
        _director = director;
    }

    public OIDDAGroupConfig Config => _config;

    public DirectorManager Director => _director;

    /// <summary> Group-level variables: what group rules read and write. </summary>
    public IValueStore Store => _store;

    // Read-only variables published to the players (Group.*).
    internal Dictionary<string, object> Variables => _variables;

    /// <summary> Aggregated difficulty score of the players at the last evaluation. </summary>
    public float Score { get; private set; } = 0.5f;

    /// <summary> Number of players taken into account at the last evaluation. </summary>
    public int PlayerCount { get; private set; }

    public DirectorState State => _director.CurrentState;
    public float Intensity => _director.CurrentIntensity;
    public float Stress => _director.StressLevel;
    public float Fatigue => _director.FatigueLevel;
    public float DifficultyMultiplier => _director.DifficultyMultiplier;
    public bool ShouldSpawnEncounter => _director.ShouldSpawnEncounter();

    /// <summary> Adds intensity that belongs to the group itself. </summary>
    public void AddIntensity(float amount, string reason = "")
    {
        _director.AddGroupBoost(amount, reason);

        if (_owner.DebugMode) Debug.Log($"[Group] Intensity added: + {amount} ({reason})");
    }

    public T GetValue<T>(string name) => _store.GetValue<T>(name);

    public void SetValue(string name, object value) => _store.SetValue(name, value);

    internal void Configure(OIDDAGroupConfig config, IEnumerable<KeyValuePair<string, object>> initialValues)
    {
        _config = config;

        // Group rules can target the same variable names as the players; new names are added when they show up.
        _store.SeedMissing(initialValues);

        var directorConfig = new OIDDAConfig
        {
            UseDDASmoothing = config.UseSmoothing,
            SmoothingSpeed = config.SmoothingSpeed,
            UseDirector = true,
            DirectorRules = config.GroupRules ?? new List<DirectorRule>()
        };
        _director.currentConfig = directorConfig;
        _director.isDirectorSmoothing = config.UseSmoothing;
    }

    internal void Reset()
    {
        _store.ResetValues();
        _director.ResetSmoothing();
        _accumulator = 0f;
    }

    internal void Update(float deltaTime, OIDDAContext defaultContext, List<OIDDAContext> extraContexts)
    {
        if (_config == null) return;

        _accumulator += deltaTime;
        if (_config.UpdateInterval > 0f && _accumulator < _config.UpdateInterval) return;

        var dt = _accumulator;
        _accumulator = 0f;

        _director.DebugMode = _owner.DebugMode;

        _scores.Clear(); _intensities.Clear(); _stresses.Clear(); _fatigues.Clear(); _weights.Clear();
        if (_config.IncludeDefaultPlayer) Collect(defaultContext);
        for (int i = 0; i < extraContexts.Count; i++) Collect(extraContexts[i]);

        var mode = _config.Aggregation;
        var percentile = _config.Percentile;
        var inputs = new GroupInputs(
            GroupAggregator.Aggregate(_scores, _weights, mode, percentile, 0.5f, _scratch),
            GroupAggregator.Aggregate(_intensities, _weights, mode, percentile, 0f, _scratch),
            GroupAggregator.Aggregate(_stresses, _weights, mode, percentile, 0f, _scratch),
            GroupAggregator.Aggregate(_fatigues, _weights, mode, percentile, 0f, _scratch));

        Score = inputs.Score;
        PlayerCount = _scores.Count;

        // Rules of the group Director see the aggregated inputs of this cycle...
        Publish(OIDDAGroupVariables.Score, inputs.Score);
        Publish(OIDDAGroupVariables.Stress, inputs.Stress);
        Publish(OIDDAGroupVariables.Fatigue, inputs.Fatigue);
        Publish(OIDDAGroupVariables.PlayerCount, PlayerCount);

        _director.OnGroupUpdate(dt, _store, inputs);

        // ...and the players see the resulting group state.
        Publish(OIDDAGroupVariables.Intensity, _director.CurrentIntensity);
        Publish(OIDDAGroupVariables.Stress, _director.StressLevel);
        Publish(OIDDAGroupVariables.Fatigue, _director.FatigueLevel);
        Publish(OIDDAGroupVariables.State, (int)_director.CurrentState);
        Publish(OIDDAGroupVariables.Multiplier, _director.DifficultyMultiplier);
    }

    void Collect(OIDDAContext player)
    {
        if (player == null || !player.HasStore) return;

        _weights.Add(Mathf.Max(0f, player.GroupWeight));

        if (player.Age < _config.JoinWarmupSeconds)
        {
            // A player who just joined is neutral for the group until the warm-up is over.
            _scores.Add(0.5f);
            _intensities.Add(0f);
            _stresses.Add(0f);
            _fatigues.Add(0f);
            return;
        }

        _scores.Add(player.EvaluateScore());
        _intensities.Add(player.Intensity);
        _stresses.Add(player.PlayerStress);
        _fatigues.Add(player.PlayerFatigue);
    }

    void Publish(string name, object value)
    {
        _variables[name] = value;
        _store.SetValue(name, value);
    }
}
