using FlaxEngine;
using OIDDA.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OIDDA.Network;

/// <summary>
/// Where a game keeps the profiles of its players (see <see cref="OIDDAProfileStore"/>): its own database, a cloud save, and so on.
/// The calls are synchronous and run on the server's game thread, so they must answer quickly. A profile is an opaque block of bytes;
/// the key is the player key (see <see cref="OIDDAPlayerKey"/>).
/// </summary>
public interface IOIDDAProfileProvider
{
    /// <summary> Stores the profile. False (or an exception) means it could not: OIDDA then uses its encrypted file instead. </summary>
    bool TrySave(string key, byte[] data);

    /// <summary> Gives the stored profile. False means there is none (or it could not be read): OIDDA then looks in its file. </summary>
    bool TryLoad(string key, out byte[] data);

    /// <summary> Deletes the stored profile. </summary>
    bool TryDelete(string key);
}

/// <summary>
/// Saves and loads the profile of a player (see <see cref="OIDDAProfile"/>) on the server. A profile goes to
/// <see cref="Provider"/> if the game set one and it succeeds; otherwise to a file, encrypted with AES-256-GCM.
/// </summary>
/// <remarks>
/// The file is named after the SHA-256 of the player key, in the "profiles" folder of OIDDA's folder, and written through a temporary
/// file so an interruption never leaves a broken profile. The AES key is derived (HKDF-SHA256, with a random salt in each file) from
/// <see cref="EncryptionKey"/>. If the game sets none, a random key is made and kept in a file next to the profiles: that only stops
/// casual reading and editing, not someone with access to the folder, so a game that cares should set its own secret.
/// A profile is tied to its player: a file copied under another player's name is refused. When a profile exists both in the
/// provider and in the file, the more recent one wins.
/// </remarks>
public static class OIDDAProfileStore
{
    const string Extension = ".oidp";
    const string SecretFile = "profile.secret";
    const byte FileVersion = 1;
    const int SaltLength = 16;
    const int NonceLength = 12;
    const int TagLength = 16;
    const int KeyLength = 32;
    const int HeaderLength = 4 + 1 + SaltLength + NonceLength;

    static readonly byte[] Magic = { (byte)'O', (byte)'I', (byte)'D', (byte)'P' };
    static readonly byte[] Info = Encoding.ASCII.GetBytes("OIDDA profile v1");
    static readonly HashSet<string> Warned = new();

    static byte[] _generated;

    // Test seams.
    internal static string FolderOverride;
    internal static bool? CryptoSupportedOverride;

    /// <summary> Where the game keeps the profiles. Null: only the encrypted file is used. </summary>
    public static IOIDDAProfileProvider Provider { get; set; }

    /// <summary>
    /// The secret the file key is derived from: 32 random bytes, kept out of the game's files (for example from a server setting).
    /// With a value that is not 32 bytes long no profile file is written or read.
    /// </summary>
    public static byte[] EncryptionKey { get; set; }

    /// <summary> Turns a passphrase into a 32-byte <see cref="EncryptionKey"/> (PBKDF2-SHA256). It is slow on purpose: call it once at start-up. </summary>
    public static byte[] KeyFromPassphrase(string passphrase, byte[] salt, int iterations = 210000)
    {
        if (string.IsNullOrEmpty(passphrase)) throw new ArgumentException("The passphrase is empty.", nameof(passphrase));
        if (salt == null || salt.Length < 8) throw new ArgumentException("The salt must be at least 8 bytes.", nameof(salt));

        return Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, iterations, HashAlgorithmName.SHA256, KeyLength);
    }

    /// <summary> Folder of the profile files. </summary>
    public static string ProfilesFolder => FolderOverride ?? Path.Combine(OIDDAPlayerKey.DefaultDirectory, "profiles");

    internal static bool CryptoSupported => CryptoSupportedOverride ?? AesGcm.IsSupported;

    /// <summary>
    /// Stores a profile. Returns false if it could not be stored anywhere (no provider that works, and no usable file).
    /// When the provider stores it, an older copy in the file is deleted so it cannot come back.
    /// </summary>
    public static bool Save(string key, byte[] profile)
    {
        if (!OIDDAPlayerKey.IsValid(key) || profile == null || profile.Length > OIDDAProfile.MaxBytes) return false;

        var provider = Provider;
        if (provider != null)
        {
            try
            {
                if (provider.TrySave(key, profile))
                {
                    DeleteFile(key);
                    return true;
                }
            }
            catch (Exception)
            {
                // Treated like a refusal.
            }

            WarnOnce("provider-save", "[OIDDA Network] The profile provider could not save a profile: the encrypted file is used instead.");
        }

        return SaveFile(key, profile);
    }

    /// <summary>
    /// Finds the profile of a player: in the provider and in the file, and the more recent of the two if both exist.
    /// </summary>
    public static bool TryLoad(string key, out byte[] profile)
    {
        profile = null;
        if (!OIDDAPlayerKey.IsValid(key)) return false;

        byte[] fromProvider = null;
        var provider = Provider;
        if (provider != null)
        {
            try
            {
                if (provider.TryLoad(key, out var data) && IsProfile(data)) fromProvider = data;
            }
            catch (Exception)
            {
                WarnOnce("provider-load", "[OIDDA Network] The profile provider failed while loading a profile.");
            }
        }

        var fromFile = LoadFile(key);

        if (fromProvider == null) profile = fromFile;
        else if (fromFile == null) profile = fromProvider;
        else profile = IsNewer(fromFile, fromProvider) ? fromFile : fromProvider;

        return profile != null;
    }

    /// <summary> Deletes the profile of a player, from the provider and from the file (for example when the account is deleted). </summary>
    public static void Delete(string key)
    {
        if (!OIDDAPlayerKey.IsValid(key)) return;

        try
        {
            Provider?.TryDelete(key);
        }
        catch (Exception)
        {
            WarnOnce("provider-delete", "[OIDDA Network] The profile provider failed while deleting a profile.");
        }

        DeleteFile(key);
    }

    /// <summary> Forgets everything kept in memory and the settings (tests). </summary>
    internal static void ResetForTests()
    {
        Provider = null;
        EncryptionKey = null;
        FolderOverride = null;
        CryptoSupportedOverride = null;
        _generated = null;
        Warned.Clear();
    }

    static void WarnOnce(string kind, string message)
    {
        if (Warned.Add(kind)) Debug.LogWarning(message);
    }

    static bool IsProfile(byte[] data)
        => data != null && data.Length <= OIDDAProfile.MaxBytes && OIDDAProfile.TryReadSavedAt(data, out _);

    static bool IsNewer(byte[] a, byte[] b)
        => OIDDAProfile.TryReadSavedAt(a, out var ta) && OIDDAProfile.TryReadSavedAt(b, out var tb) && ta > tb;

    static byte[] KeyHash(string key) => SHA256.HashData(Encoding.UTF8.GetBytes(key));

    static string PathFor(string key) => Path.Combine(ProfilesFolder, Convert.ToHexString(KeyHash(key)).ToLowerInvariant() + Extension);

    static void DeleteFile(string key)
    {
        try
        {
            File.Delete(PathFor(key));
        }
        catch (Exception)
        {
            // Not deleted: a newer provider profile still wins by its date.
        }
    }

    static bool SaveFile(string key, byte[] profile)
    {
        if (!CryptoSupported)
        {
            WarnOnce("no-crypto", "[OIDDA Network] AES-GCM is not available on this platform: profiles cannot be written to a file.");
            return false;
        }

        try
        {
            bool generated = EncryptionKey == null;
            var secret = Secret(create: true);
            if (secret == null)
            {
                // A secret of the wrong size has already been reported.
                if (generated) WarnOnce("no-secret", "[OIDDA Network] No usable encryption key: profiles are not written to a file.");
                return false;
            }

            if (generated)
                WarnOnce("generated-key", "[OIDDA Network] OIDDAProfileStore.EncryptionKey is not set: profile files use a key kept next to them. That only stops casual reading and editing; set your own secret to protect them.");

            WriteAtomic(PathFor(key), Seal(profile, secret, KeyHash(key)));
            return true;
        }
        catch (Exception e)
        {
            WarnOnce("file-save", $"[OIDDA Network] Could not write a profile file: {e.Message}");
            return false;
        }
    }

    static byte[] LoadFile(string key)
    {
        try
        {
            var path = PathFor(key);
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > OIDDAProfile.MaxBytes + HeaderLength + TagLength || !CryptoSupported) return null;

            var secret = Secret(create: false);
            if (secret == null) return null;

            return TryOpen(File.ReadAllBytes(path), secret, KeyHash(key), out var plain) && IsProfile(plain) ? plain : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    static byte[] Secret(bool create)
    {
        var provided = EncryptionKey;
        if (provided != null)
        {
            if (provided.Length == KeyLength) return provided;

            WarnOnce("bad-key", "[OIDDA Network] OIDDAProfileStore.EncryptionKey must be 32 bytes: profile files are not used.");
            return null;
        }

        return _generated ??= LoadOrCreateSecret(create);
    }

    static byte[] LoadOrCreateSecret(bool create)
    {
        var path = Path.Combine(ProfilesFolder, SecretFile);

        try
        {
            if (File.Exists(path))
            {
                var saved = File.ReadAllBytes(path);
                if (saved.Length == KeyLength) return saved;
            }

            if (!create) return null;

            var secret = RandomNumberGenerator.GetBytes(KeyLength);
            WriteAtomic(path, secret);
            return secret;
        }
        catch (Exception)
        {
            return null;
        }
    }

    static void WriteAtomic(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var temp = path + ".tmp";

        try
        {
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, path, true);
        }
        catch (Exception)
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception)
            {
                // Nothing more to do.
            }

            throw;
        }
    }

    // File layout: magic (4), version (1), salt (16), nonce (12), ciphertext, tag (16). The header and the hash of the player key are
    // authenticated, so a file cannot be changed or moved to another player.
    internal static byte[] Seal(byte[] plain, byte[] secret, byte[] keyHash)
    {
        var blob = new byte[HeaderLength + plain.Length + TagLength];
        Buffer.BlockCopy(Magic, 0, blob, 0, Magic.Length);
        blob[4] = FileVersion;
        RandomNumberGenerator.Fill(blob.AsSpan(5, SaltLength));
        RandomNumberGenerator.Fill(blob.AsSpan(5 + SaltLength, NonceLength));

        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, secret, KeyLength, blob.AsSpan(5, SaltLength).ToArray(), Info);
        using var aes = new AesGcm(key, TagLength);
        aes.Encrypt(blob.AsSpan(5 + SaltLength, NonceLength), plain, blob.AsSpan(HeaderLength, plain.Length), blob.AsSpan(HeaderLength + plain.Length, TagLength), Aad(blob, keyHash));
        return blob;
    }

    internal static bool TryOpen(byte[] blob, byte[] secret, byte[] keyHash, out byte[] plain)
    {
        plain = null;
        if (blob == null || blob.Length < HeaderLength + TagLength) return false;
        for (int i = 0; i < Magic.Length; i++)
            if (blob[i] != Magic[i]) return false;
        if (blob[4] != FileVersion) return false;

        int length = blob.Length - HeaderLength - TagLength;
        try
        {
            var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, secret, KeyLength, blob.AsSpan(5, SaltLength).ToArray(), Info);
            using var aes = new AesGcm(key, TagLength);
            var result = new byte[length];
            aes.Decrypt(blob.AsSpan(5 + SaltLength, NonceLength), blob.AsSpan(HeaderLength, length), blob.AsSpan(HeaderLength + length, TagLength), result, Aad(blob, keyHash));
            plain = result;
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    static byte[] Aad(byte[] blob, byte[] keyHash)
    {
        var aad = new byte[HeaderLength + keyHash.Length];
        Buffer.BlockCopy(blob, 0, aad, 0, HeaderLength);
        Buffer.BlockCopy(keyHash, 0, aad, HeaderLength, keyHash.Length);
        return aad;
    }
}
