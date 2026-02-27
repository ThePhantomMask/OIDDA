using FlaxEngine;
using OIDDA.Data;

namespace OIDDA;

/// <summary>
/// Director Utils
/// </summary>
public static class DirectorUtils
{
    public static float CalculateScoreByDirectorValue(DirectorValue value)
    {
        var raw = CalculateScore(value.Value.Value);
        var normalized = Mathf.Saturate((raw - CalculateScore(value.Min.Value))/ (CalculateScore(value.Max.Value) - CalculateScore(value.Min.Value)));
        return (value.Action is DirectorAction.Increase) ? normalized : - normalized;
    }

    static float CalculateScore(object gameplayValue) => gameplayValue switch
    {
        float f => f,
        int i => (float)i,
        bool b => b ? 1f : 0f,
        Vector2 v2 => v2.Length,
        Vector3 v3 => v3.Length,
        Vector4 v4 => v4.Length,
        Quaternion q => q.Length,
        Color c => c.ValuesSum,
        Transform t => t.Translation.Length,
        Matrix m => m.TranslationVector.Length,
        _ => 0f
    };
}
