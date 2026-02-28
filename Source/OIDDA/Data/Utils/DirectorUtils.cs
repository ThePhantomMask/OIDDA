using FlaxEngine;
using System;

namespace OIDDA;

/// <summary>
/// Director Utils
/// </summary>
public static class DirectorUtils
{

    public static float CalculateScore(object currentValue)
    {
        try
        {
            return ConvertToFloat(currentValue);
        }
        catch (NullReferenceException e)
        {
            Debug.LogException(e);
            return 1f;
        }
    }

    static float ConvertToFloat(object value) => value switch
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
