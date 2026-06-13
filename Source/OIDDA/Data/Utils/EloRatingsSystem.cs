using System;
using System.Collections.Generic;
using FlaxEngine;

namespace OIDDA.Elo;

/// <summary>
/// Possible outcomes of a "match" from the player's perspective.
/// </summary>
public enum MatchResult
{
    Loss = 0,
    Draw = 1,
    Win = 2
}

/// <summary>
/// Pure ELO rating logic, so it can be unit-tested and reused by different ORS agents.
/// Implements the classic ELO formula:
///   E_A = 1 / (1 + 10^((R_B - R_A) / 400))
///   R_A' = R_A + K * (S_A - E_A)
///
/// The K-factor is dynamic (chess-style): it starts high while the system has little information about the player and decreases as more matches are recorded, so the rating converges and stabilizes.
/// </summary>
public class EloRatingsSystem
{
    /// <summary> Rating used for a brand-new player/entity. </summary>
    public float InitialRating = 1000f;
    /// <summary> K-factor used while GamesPlayed and KFactorRampGames (provisional period). </summary>
    public float KFactorProvisional = 32f;
    /// <summary> K-factor used after GamesPlayed and KFactorRampGames (stable period).</summary>
    public float KFactorStable = 12f;
    /// <summary> Number of recorded matches after which the player switches from the provisional K-factor to the stable one. </summary>
    public int KFactorRampGames = 20;
    /// <summary> Standard ELO divisor (400 = roughly the rating gap that corresponds to a 10x difference in odds). </summary>
    public float RatingDivisor = 400f;
    /// <summary>Current player rating. </summary>
    public float PlayerRating { get; private set; }
    /// <summary> Total number of matches recorded for the player (used for the dynamic K-factor). </summary>
    public float GamesPlayed { get; private set; }

    public EloRatingsSystem(float? startingRating = null)
    {
        PlayerRating = startingRating ?? InitialRating;
    }

    /// <summary>
    /// Resets the system to the initial rating with zero games played.
    /// </summary>
    public void Reset(float? startingRating = null)
    {
        PlayerRating = startingRating ?? InitialRating;
        GamesPlayed = 0;
    }

    /// <summary>
    /// Expected score (probability of winning, in [0,1]) of "A" against "B" given their current ratings.
    /// </summary>
    public float ExpectedScore(float ratingA, float ratingB)
    {
        var exponent = (ratingB - ratingA) / RatingDivisor;
        return 1f / (1f + MathF.Pow(10f, exponent));
    }


}
