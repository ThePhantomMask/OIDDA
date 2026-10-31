using FlaxEngine;

namespace OIDDA.Data;

/// <summary>
/// Turns a game value into the single number OIDDA works with. The same conversion is used when a metric is scored on the
/// server and when a client reports a metric to the server, so a value gives the same number on both paths.
/// </summary>
/// <remarks>
/// Conversions: float and int as they are; bool is 1 or 0; Vector2, Vector3, Vector4 and Quaternion use their length; Color uses
/// the sum of its channels; Transform and Matrix use the length of their translation. Quaternion length is about 1 for any unit
/// quaternion, so it carries little information. Any other type has no number.
/// </remarks>
public static class OIDDAValue
{
    /// <summary>
    /// Converts a value to a float. Returns false (and 0) when the type has no numeric meaning.
    /// </summary>
    public static bool TryToFloat(object value, out float number)
    {
        // Lengths are cast because the engine's vector length is a double in projects built with large worlds.
        switch (value)
        {
            case float f: number = f; return true;
            case int i: number = i; return true;
            case bool b: number = b ? 1f : 0f; return true;
            case Vector2 v2: number = (float)v2.Length; return true;
            case Vector3 v3: number = (float)v3.Length; return true;
            case Vector4 v4: number = (float)v4.Length; return true;
            case Quaternion q: number = (float)q.Length; return true;
            case Color c: number = c.ValuesSum; return true;
            case Transform t: number = (float)t.Translation.Length; return true;
            case Matrix m: number = (float)m.TranslationVector.Length; return true;
            default: number = 0f; return false;
        }
    }

    /// <summary>
    /// Converts a value to a float, or 0 when the type has no numeric meaning.
    /// </summary>
    public static float ToFloat(object value) => TryToFloat(value, out var number) ? number : 0f;
}
