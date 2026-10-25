using OIDDA.Data;
using System;
using System.Collections.Generic;
using System.IO;

namespace OIDDA.Network;

/// <summary>
/// Compact binary format of the state a server sends to a client: some players' snapshots and optionally the group's.
/// Decoding checks everything, so a malformed message is rejected instead of corrupting the client's state.
/// </summary>
public static class OIDDANetworkCodec
{
    public const byte Version = 1;
    public const int MaxPlayersPerMessage = 256;

    const byte GroupFlag = 1;
    const int HeaderBytes = 1 + 1 + 2;
    const int SnapshotBytes = 1 + 5 * 4 + 1 + 2;

    public static byte[] Encode(IReadOnlyList<KeyValuePair<uint, OIDDAStateSnapshot>> players, OIDDAStateSnapshot? group)
    {
        int count = Math.Min(players.Count, MaxPlayersPerMessage);
        int size = HeaderBytes + count * (4 + SnapshotBytes) + (group.HasValue ? SnapshotBytes : 0);

        using var stream = new MemoryStream(size);
        using var writer = new BinaryWriter(stream);

        writer.Write(Version);
        writer.Write(group.HasValue ? GroupFlag : (byte)0);
        writer.Write((ushort)count);

        for (int i = 0; i < count; i++)
        {
            writer.Write(players[i].Key);
            WriteSnapshot(writer, players[i].Value);
        }

        if (group.HasValue) WriteSnapshot(writer, group.Value);

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>
    /// Decodes a message. On failure the output is empty and false is returned.
    /// </summary>
    public static bool TryDecode(byte[] data, Dictionary<uint, OIDDAStateSnapshot> players, out OIDDAStateSnapshot? group)
    {
        players.Clear();
        group = null;

        if (data == null || data.Length < HeaderBytes || data[0] != Version) return false;

        byte flags = data[1];
        if ((flags & ~GroupFlag) != 0) return false;

        bool hasGroup = (flags & GroupFlag) != 0;
        int count = data[2] | (data[3] << 8);
        if (count > MaxPlayersPerMessage) return false;

        int expected = HeaderBytes + count * (4 + SnapshotBytes) + (hasGroup ? SnapshotBytes : 0);
        if (data.Length != expected) return false;

        using var reader = new BinaryReader(new MemoryStream(data, HeaderBytes, data.Length - HeaderBytes));

        for (int i = 0; i < count; i++)
        {
            uint id = reader.ReadUInt32();
            if (!TryReadSnapshot(reader, out var snapshot))
            {
                players.Clear();
                return false;
            }
            players[id] = snapshot;
        }

        if (hasGroup)
        {
            if (!TryReadSnapshot(reader, out var groupSnapshot))
            {
                players.Clear();
                return false;
            }
            group = groupSnapshot;
        }

        return true;
    }

    static void WriteSnapshot(BinaryWriter writer, OIDDAStateSnapshot s)
    {
        writer.Write((byte)s.State);
        writer.Write(s.Intensity);
        writer.Write(s.Stress);
        writer.Write(s.Fatigue);
        writer.Write(s.Score);
        writer.Write(s.DifficultyMultiplier);
        writer.Write(s.ShouldSpawnEncounter ? (byte)1 : (byte)0);
        writer.Write((ushort)Math.Clamp(s.PlayerCount, 0, ushort.MaxValue));
    }

    static bool TryReadSnapshot(BinaryReader reader, out OIDDAStateSnapshot snapshot)
    {
        snapshot = default;

        byte state = reader.ReadByte();
        if (!Enum.IsDefined(typeof(DirectorState), (int)state)) return false;

        float intensity = reader.ReadSingle();
        float stress = reader.ReadSingle();
        float fatigue = reader.ReadSingle();
        float score = reader.ReadSingle();
        float multiplier = reader.ReadSingle();

        byte flags = reader.ReadByte();
        if ((flags & ~1) != 0) return false;

        ushort playerCount = reader.ReadUInt16();

        snapshot = new OIDDAStateSnapshot
        {
            State = (DirectorState)state,
            Intensity = Sanitize(intensity, 0f, 100f),
            Stress = Sanitize(stress, 0f, 100f),
            Fatigue = Sanitize(fatigue, 0f, 100f),
            Score = Sanitize(score, 0f, 1f),
            DifficultyMultiplier = Sanitize(multiplier, 0f, 10f),
            ShouldSpawnEncounter = (flags & 1) != 0,
            PlayerCount = playerCount
        };
        return true;
    }

    // NaN and infinity become 0; everything else is kept inside the range the value can legitimately have.
    static float Sanitize(float value, float min, float max) => float.IsFinite(value) ? Math.Clamp(value, min, max) : 0f;
}
