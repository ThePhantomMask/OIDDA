using System;

namespace OIDDA;

/// <summary>
/// ORSUtils class.
/// </summary>
public class ORSUtils
{
    /// <summary>
    /// Returns a new unique ID on every read.
    /// </summary>
    public static string GeneratedID => Guid.NewGuid().ToString("N");
}
