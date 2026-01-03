using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// Represents an abstract base class for an DRS (Director Receiver Sender) agent, providing methods exchanging values with an DRS system.
/// </summary>
/// <remarks>This class defines the core contract for interacting with DRS agents, including value transmission. Derived classes should implement the abstract connection methods to provide specific ORS
/// agent behaviors. Thread safety and connection state management are the responsibility of the implementing class.</remarks>
public abstract class DRSAgent
{
    public abstract void AddPacingIntensity(float amount, string reason = "");
}

/// <summary>
/// Director Receiver Sender Agent
/// </summary>
public class DRS : DRSAgent
{
    public static DRS Instance = new();

    /// <summary>
    /// Adds the specified amount of pacing intensity to the pacing director, optionally providing a reason for the adjustment.
    /// </summary>
    /// <param name="amount">The amount of pacing intensity to add. Positive values increase pacing intensity.</param>
    /// <param name="reason">An optional description of the reason for the intensity adjustment. This value may be used for logging or debugging purposes.</param>
    public override void AddPacingIntensity(float amount, string reason = "")
    {
        if (!OIDDAUtils.OIDDAManager) return;
        OIDDAUtils.OIDDAManager.AddPacingIntensity(amount, reason);
    }

    /// <summary>
    /// Gets a value indicating whether an encounter should be spawned based on the current pacing settings.
    /// </summary>
    /// <remarks>If pacing is enabled, this property reflects the recommendation of the pacing director. If pacing is disabled, it always returns <see langword="true"/>.</remarks>
    public bool IsShouldSpawnEncounter => (OIDDAUtils.OIDDAManager) ? OIDDAUtils.OIDDAManager.IsShouldSpawnEncounter : false;
    /// <summary>
    /// Gets the current pacing state of the director.
    /// </summary>
    public PacingDirector.PacingState PacingState => (OIDDAUtils.OIDDAManager) ? OIDDAUtils.OIDDAManager.DirectorState : PacingDirector.PacingState.Build;
    /// <summary>
    /// Gets the current stress level of the player as determined by the Pacing Director.
    /// </summary>
    public float Stress => (OIDDAUtils.OIDDAManager) ? OIDDAUtils.OIDDAManager.PlayerStress : 0.0f;
    /// <summary>
    /// Gets the current fatigue level of the player.
    /// </summary>
    public float Fatigue => (OIDDAUtils.OIDDAManager) ? OIDDAUtils.OIDDAManager.PlayerFatigue : 0.0f;
}