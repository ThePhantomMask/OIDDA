using System;
using System.IO;
using OIDDA;

namespace OIDDA.Network;

/// <summary>
/// The secret a client sends to the server to be recognised when it reconnects. The engine gives a client a new id every time it
/// connects, so the id cannot identify a returning player. By default the key is a random 128-bit value created on first use and
/// kept in a small file on the client; it is not related to the user. A game with accounts can supply its own through
/// <see cref="Provider"/>.
/// </summary>
public static class OIDDAPlayerKey
{
    public const int MinLength = 8;
    public const int MaxLength = 64;
    const string FileName = "player.key";

    static string _cached;

    /// <summary>
    /// Supplies the key instead of the generated one (for example an account id). Must return a key that passes <see cref="IsValid"/>,
    /// otherwise the generated key is used.
    /// </summary>
    public static Func<string> Provider { get; set; }

    /// <summary> A key is 8 to 64 letters, digits, '-' or '_'. </summary>
    public static bool IsValid(string key)
    {
        if (key == null || key.Length < MinLength || key.Length > MaxLength) return false;

        foreach (var c in key)
        {
            bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
            if (!ok) return false;
        }
        return true;
    }

    /// <summary>
    /// The key to send: the provider's if it gives a valid one, otherwise the generated key (created and saved on first use).
    /// </summary>
    public static string Current(string directory = null)
    {
        var custom = Provider?.Invoke();
        if (IsValid(custom)) return custom;

        return _cached ??= LoadOrCreate(directory ?? DefaultDirectory);
    }

    /// <summary> Folder of the key file: the user's local application data. </summary>
    public static string DefaultDirectory => OIDDAPlugin.Instance.FolderPath;

    /// <summary>
    /// Reads the saved key, or creates and saves a new one. If the file cannot be written the key still works for this session.
    /// </summary>
    public static string LoadOrCreate(string directory)
    {
        var path = Path.Combine(directory, FileName);

        try
        {
            if (File.Exists(path))
            {
                var saved = File.ReadAllText(path).Trim();
                if (IsValid(saved)) return saved;
            }
        }
        catch (Exception)
        {
            // Unreadable: a new key is created below.
        }

        var key = Guid.NewGuid().ToString("N");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, key);
        }
        catch (Exception)
        {
            // Not saved: the key only lasts as long as this process.
        }

        return key;
    }

    /// <summary> Forgets the key kept in memory (tests, or after the provider changes). </summary>
    public static void ResetCache() => _cached = null;
}
