namespace OIDDA.Data;

/// <summary>
/// Default thresholds shared by metrics, manager and aggregator.
/// </summary>
public static class OIDDADefaults
{
    /// <summary> Scores below this value mean the game is too easy. </summary>
    public const float EasyThreshold = 0.3f;

    /// <summary> Scores between <see cref="EasyThreshold"/> and this value are considered a warning band for a single metric. </summary>
    public const float WarningThreshold = 0.5f;

    /// <summary> Scores above this value mean the game is too difficult. </summary>
    public const float DifficultThreshold = 0.7f;
}
