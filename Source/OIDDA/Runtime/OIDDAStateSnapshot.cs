using OIDDA.Data;

namespace OIDDA;

/// <summary>
/// What a client needs to know about a player's (or the group's) pacing state: enough for a HUD, music or effects.
/// Built on the server (<see cref="From(OIDDAContext)"/>, <see cref="From(OIDDAGroup)"/>) and handed to clients by an
/// <see cref="IOIDDAStateProvider"/>. It is a small plain struct, easy to send over the network.
/// </summary>
public struct OIDDAStateSnapshot
{
    public DirectorState State;
    public float Intensity;
    public float Stress;
    public float Fatigue;

    /// <summary> Difficulty score, 0 = very easy .. 1 = very difficult. </summary>
    public float Score;

    /// <summary> Difficulty multiplier suggested by the pacing. </summary>
    public float DifficultyMultiplier;

    public bool ShouldSpawnEncounter;

    /// <summary> Players taken into account: 1 for a player, the group size for the group. </summary>
    public int PlayerCount;

    public static OIDDAStateSnapshot From(OIDDAContext player) => new()
    {
        State = player.DirectorState,
        Intensity = player.Intensity,
        Stress = player.PlayerStress,
        Fatigue = player.PlayerFatigue,
        Score = player.EvaluateScore(),
        DifficultyMultiplier = player.Director.DifficultyMultiplier,
        ShouldSpawnEncounter = player.IsShouldSpawnEncounter,
        PlayerCount = 1
    };

    public static OIDDAStateSnapshot From(OIDDAGroup group) => new()
    {
        State = group.State,
        Intensity = group.Intensity,
        Stress = group.Stress,
        Fatigue = group.Fatigue,
        Score = group.Score,
        DifficultyMultiplier = group.DifficultyMultiplier,
        ShouldSpawnEncounter = group.ShouldSpawnEncounter,
        PlayerCount = group.PlayerCount
    };
}
