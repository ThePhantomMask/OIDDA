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

    /// <summary>
    /// Returns the K-factor to use for the player's NEXT update, based on how many matches have been recorded so far.
    /// </summary>
    public float CurrentKFactor => GamesPlayed < KFactorRampGames ? KFactorProvisional : KFactorStable;

    /// <summary>
    /// Converts a MatchResult into the ELO "score" S_A used in the formula (1 = win, 0.5 = draw, 0 = loss).
    /// </summary>
    public static float ResultToScore(MatchResult result) => result switch
    {
        MatchResult.Win => 1f,
        MatchResult.Draw => 0.5f,
        _ => 0,
    };


    /// <summary>
    /// Records the outcome of a single match between the player (rating = PlayerRating) and an opponent with the given rating, updating the player's rating in place.
    /// Returns the new player rating and, via <paramref name="newOpponentRating"/>, the opponent's new rating (useful if the opponent also has a persistent rating, e.g. an enemy archetype or an encounter).
    /// </summary>
    public float RecordMatch(float opponentRating, MatchResult result, out float newOpponentRating, bool updateOpponent = true)
    {
        var score = ResultToScore(result);
        var expectedPlayer = ExpectedScore(PlayerRating, opponentRating);
        float k = CurrentKFactor;

        var newPlayerRating = PlayerRating + k * (score - expectedPlayer);

        if (updateOpponent)
        {
            var expectedOpponent = ExpectedScore(opponentRating, PlayerRating);
            var opponentScore = 1f - score; // zero-sum
            // Opponents (enemies/encounters) use the stable K-factor by convention, since they don't track their own "games played".
            newOpponentRating = opponentRating + KFactorStable * (opponentScore - expectedOpponent);
        }
        else
        {
            newOpponentRating = opponentRating;
        }

        PlayerRating = newPlayerRating;
        GamesPlayed += Time.DeltaTime;
        return PlayerRating;
    }
}

/// <summary>
/// Keeps a small in-memory table of ratings for "opponents": either individual enemy archetypes (e.g. "Goblin", "Sniper") or aggregated encounters/levels (e.g. "Level_03_Boss"). 
/// Each entry is a simple ELO rating that evolves the same way the player's rating does.
/// </summary>
public class EloOpponentPool
{
    readonly Dictionary<string, float> ratings = new();

    /// <summary> Default rating assigned to an opponent the first time it's seen. </summary>
    public float DefaultRating = 1000f;

    /// <summary>
    /// Gets the current rating for an opponent id, creating it with <see cref="DefaultRating"/> if it doesn't exist yet.
    /// </summary>
    public float GetRating(string opponentId)
    {
        if (!ratings.TryGetValue(opponentId, out var rating))
        {
            rating = DefaultRating;
            ratings[opponentId] = rating;
        }
        return rating;
    }

    /// <summary> Overwrites the stored rating for an opponent id. </summary>
    public void SetRating(string opponentId, float rating) => ratings[opponentId] = rating;
    /// <summary> Removes all stored ratings (in-memory only, no persistence). </summary>
    public void Clear() => ratings.Clear();

    public IReadOnlyDictionary<string, float> Ratings => ratings;
}