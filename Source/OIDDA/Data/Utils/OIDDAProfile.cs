using FlaxEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace OIDDA.Data;

/// <summary>
/// The part of a player's state that is kept between sessions: their values, their personal Director and their age. It is written
/// as one small, versioned block of bytes. Group state and the group weight are not part of it (the game sets those at run time),
/// and neither are the smoothing, the delays and the timers.
/// </summary>
/// <remarks>
/// Numbers are stored as 32-bit floats, so a position keeps float precision even with large worlds. A texture is stored as its
/// asset id and loaded again when the profile is restored; a texture that was created at run time (virtual) or is missing is left out.
/// On restore a value is applied only if the player already has a value of the same type under that name, or none at all.
/// </remarks>
public static class OIDDAProfile
{
    /// <summary> Version of the byte format. A block with another version is ignored. </summary>
    public const byte Version = 1;

    public const int MaxValues = 256;
    public const int MaxNameLength = 64;
    public const int MaxStringLength = 4096;

    /// <summary> Biggest profile, in bytes: a bigger one is not written and is not read. </summary>
    public const int MaxBytes = 64 * 1024;

    // Written to disk: never renumber.
    enum Tag : byte
    {
        Float = 1, Int = 2, Bool = 3, String = 4, Vector2 = 5, Vector3 = 6, Vector4 = 7, Color = 8, Quaternion = 9, Transform = 10,
        BoundingBox = 11, BoundingSphere = 12, Rectangle = 13, Matrix = 14, Texture = 15, CubeTexture = 16
    }

    /// <summary>
    /// The profile of a player, or null if it cannot be made (no store, or bigger than <see cref="MaxBytes"/>). Values of a type
    /// that cannot be saved, with an empty or too long name, or beyond <see cref="MaxValues"/>, are left out.
    /// </summary>
    public static byte[] Capture(OIDDAContext context)
    {
        if (context == null || context.Store == null || !context.Store.IsValid || context.Director == null) return null;

        var director = context.Director;
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);

        w.Write(Version);
        w.Write(DateTime.UtcNow.Ticks);
        w.Write(context.Age);
        w.Write((byte)director.CurrentState);
        w.Write(director.CurrentIntensity);
        w.Write(director.StressLevel);
        w.Write(director.FatigueLevel);
        w.Write(director.EngagementLevel);
        w.Write(director.StateTimer);

        w.Flush();
        long countAt = stream.Position;
        w.Write((ushort)0);

        int count = 0;
        foreach (var pair in context.Store.Values)
        {
            if (count >= MaxValues) break;
            if (string.IsNullOrEmpty(pair.Key) || pair.Key.Length > MaxNameLength) continue;

            w.Flush();
            long start = stream.Position;
            if (TryWrite(w, pair.Key, pair.Value))
            {
                count++;
                continue;
            }

            w.Flush();
            stream.SetLength(start);
            stream.Position = start;
        }

        w.Flush();
        stream.Position = countAt;
        w.Write((ushort)count);
        w.Flush();

        return stream.Length > MaxBytes ? null : stream.ToArray();
    }

    /// <summary>
    /// Puts a profile back into a player: the Director state, the age and the values. Nothing is applied unless the whole block is
    /// valid (version, sizes, finite numbers, known types).
    /// </summary>
    public static bool TryRestore(OIDDAContext context, byte[] data)
    {
        if (context == null || context.Store == null || context.Director == null || data == null || data.Length < 2 || data.Length > MaxBytes) return false;

        try
        {
            using var r = new BinaryReader(new MemoryStream(data, false));
            if (r.ReadByte() != Version) return false;

            r.ReadInt64(); // when it was saved
            float age = r.ReadSingle();
            byte state = r.ReadByte();
            float intensity = r.ReadSingle(), stress = r.ReadSingle(), fatigue = r.ReadSingle(), engagement = r.ReadSingle(), timer = r.ReadSingle();

            if (!float.IsFinite(age) || !float.IsFinite(intensity) || !float.IsFinite(stress) || !float.IsFinite(fatigue) || !float.IsFinite(engagement) || !float.IsFinite(timer)) return false;
            if (!Enum.IsDefined(typeof(DirectorState), (int)state)) return false;

            int count = r.ReadUInt16();
            if (count > MaxValues) return false;

            var entries = new List<KeyValuePair<string, object>>(count);
            for (int i = 0; i < count; i++)
            {
                var name = r.ReadString();
                if (name.Length == 0 || name.Length > MaxNameLength) return false;
                if (!TryRead(r, out var value)) return false;
                entries.Add(new KeyValuePair<string, object>(name, value));
            }

            if (r.BaseStream.Position != r.BaseStream.Length) return false;

            context.Director.RestoreState((DirectorState)state, Mathf.Clamp(intensity, 0f, 100f), Mathf.Clamp(stress, 0f, 100f),
                Mathf.Clamp(fatigue, 0f, 100f), Mathf.Clamp(engagement, 0f, 100f), Mathf.Clamp(timer, 0f, 1000000f));
            context.RestoreAge(Mathf.Clamp(age, 0f, 1000000000f));

            var store = context.Store;
            var current = store.Values;
            foreach (var entry in entries)
            {
                if (entry.Value == null) continue; // a texture that could not be loaded
                if (current.TryGetValue(entry.Key, out var existing) && existing != null && !SameKind(existing, entry.Value)) continue;
                store.SetValue(entry.Key, entry.Value);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary> When a profile was saved (UTC ticks), or false if the block is not a profile of this version. </summary>
    public static bool TryReadSavedAt(byte[] data, out long utcTicks)
    {
        utcTicks = 0;
        if (data == null || data.Length < 9 || data[0] != Version) return false;

        utcTicks = BitConverter.ToInt64(data, 1);
        return utcTicks > 0;
    }

    static bool SameKind(object existing, object restored)
        => existing.GetType() == restored.GetType()
           || (existing is Texture && restored is Texture)
           || (existing is CubeTexture && restored is CubeTexture);

    static void Head(BinaryWriter w, string name, Tag tag)
    {
        w.Write(name);
        w.Write((byte)tag);
    }

    static void Floats(BinaryWriter w, params float[] values)
    {
        foreach (var v in values) w.Write(v);
    }

    static bool TryWrite(BinaryWriter w, string name, object value)
    {
        switch (value)
        {
            case float f: Head(w, name, Tag.Float); w.Write(f); return true;
            case int i: Head(w, name, Tag.Int); w.Write(i); return true;
            case bool b: Head(w, name, Tag.Bool); w.Write(b); return true;
            case string s:
                if (s.Length > MaxStringLength) return false;
                Head(w, name, Tag.String); w.Write(s); return true;
            case Vector2 v2: Head(w, name, Tag.Vector2); Floats(w, (float)v2.X, (float)v2.Y); return true;
            case Vector3 v3: Head(w, name, Tag.Vector3); Floats(w, (float)v3.X, (float)v3.Y, (float)v3.Z); return true;
            case Vector4 v4: Head(w, name, Tag.Vector4); Floats(w, (float)v4.X, (float)v4.Y, (float)v4.Z, (float)v4.W); return true;
            case Color c: Head(w, name, Tag.Color); Floats(w, c.R, c.G, c.B, c.A); return true;
            case Quaternion q: Head(w, name, Tag.Quaternion); Floats(w, q.X, q.Y, q.Z, q.W); return true;
            case Transform t:
                Head(w, name, Tag.Transform);
                Floats(w, (float)t.Translation.X, (float)t.Translation.Y, (float)t.Translation.Z, t.Orientation.X, t.Orientation.Y, t.Orientation.Z, t.Orientation.W, t.Scale.X, t.Scale.Y, t.Scale.Z);
                return true;
            case BoundingBox bb:
                Head(w, name, Tag.BoundingBox);
                Floats(w, (float)bb.Minimum.X, (float)bb.Minimum.Y, (float)bb.Minimum.Z, (float)bb.Maximum.X, (float)bb.Maximum.Y, (float)bb.Maximum.Z);
                return true;
            case BoundingSphere bs:
                Head(w, name, Tag.BoundingSphere);
                Floats(w, (float)bs.Center.X, (float)bs.Center.Y, (float)bs.Center.Z, (float)bs.Radius);
                return true;
            case Rectangle rc: Head(w, name, Tag.Rectangle); Floats(w, rc.X, rc.Y, rc.Width, rc.Height); return true;
            case Matrix m:
                Head(w, name, Tag.Matrix);
                Floats(w, m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);
                return true;
            case Texture tex:
                if (!tex || tex.IsVirtual) return false;
                Head(w, name, Tag.Texture); w.Write(tex.ID.ToByteArray()); return true;
            case CubeTexture cube:
                if (!cube || cube.IsVirtual) return false;
                Head(w, name, Tag.CubeTexture); w.Write(cube.ID.ToByteArray()); return true;
            default:
                return false;
        }
    }

    static float F(BinaryReader r) => r.ReadSingle();

    // False when the block is not valid. A texture that cannot be loaded gives true and a null value (it is skipped).
    static bool TryRead(BinaryReader r, out object value)
    {
        value = null;
        var tag = (Tag)r.ReadByte();
        switch (tag)
        {
            case Tag.Float: value = r.ReadSingle(); break;
            case Tag.Int: value = r.ReadInt32(); break;
            case Tag.Bool: value = r.ReadBoolean(); break;
            case Tag.String:
                var s = r.ReadString();
                if (s.Length > MaxStringLength) return false;
                value = s; break;
            case Tag.Vector2: value = new Vector2(F(r), F(r)); break;
            case Tag.Vector3: value = new Vector3(F(r), F(r), F(r)); break;
            case Tag.Vector4: value = new Vector4(F(r), F(r), F(r), F(r)); break;
            case Tag.Color: value = new Color(F(r), F(r), F(r), F(r)); break;
            case Tag.Quaternion: value = new Quaternion(F(r), F(r), F(r), F(r)); break;
            case Tag.Transform:
                var position = new Vector3(F(r), F(r), F(r));
                var rotation = new Quaternion(F(r), F(r), F(r), F(r));
                value = new Transform(position, rotation, new Float3(F(r), F(r), F(r)));
                break;
            case Tag.BoundingBox:
                var min = new Vector3(F(r), F(r), F(r));
                value = new BoundingBox(min, new Vector3(F(r), F(r), F(r)));
                break;
            case Tag.BoundingSphere:
                var center = new Vector3(F(r), F(r), F(r));
                value = new BoundingSphere(center, F(r));
                break;
            case Tag.Rectangle: value = new Rectangle(F(r), F(r), F(r), F(r)); break;
            case Tag.Matrix:
                value = new Matrix(F(r), F(r), F(r), F(r), F(r), F(r), F(r), F(r), F(r), F(r), F(r), F(r), F(r), F(r), F(r), F(r));
                break;
            case Tag.Texture: value = Content.LoadAsync<Texture>(new Guid(r.ReadBytes(16))); break;
            case Tag.CubeTexture: value = Content.LoadAsync<CubeTexture>(new Guid(r.ReadBytes(16))); break;
            default: return false;
        }

        return true;
    }
}
