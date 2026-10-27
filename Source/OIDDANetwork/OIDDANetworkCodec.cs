using OIDDA.Data;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace OIDDA.Network;

/// <summary>
/// Compact binary format of the state a server sends to a client: some players' snapshots and optionally the group's.
/// Decoding checks everything, so a malformed message is rejected instead of corrupting the client's state.
/// Every message is written straight into one array of exactly the right size.
/// </summary>
public static class OIDDANetworkCodec
{
    public const byte Version = 1;
    public const int MaxPlayersPerMessage = 256;

    const byte GroupFlag = 1;
    const int HeaderBytes = 1 + 1 + 2;
    const int SnapshotBytes = 1 + 5 * 4 + 1 + 2;
    const int PlayerBytes = 4 + SnapshotBytes;

    public static byte[] Encode(IReadOnlyList<KeyValuePair<uint, OIDDAStateSnapshot>> players, OIDDAStateSnapshot? group)
    {
        int count = Math.Min(players.Count, MaxPlayersPerMessage);
        var data = new byte[HeaderBytes + count * PlayerBytes + (group.HasValue ? SnapshotBytes : 0)];
        int offset = WriteHeader(data, count, group.HasValue);

        for (int i = 0; i < count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), players[i].Key);
            offset += 4;
            offset = WriteSnapshot(data, offset, players[i].Value);
        }

        if (group.HasValue) WriteSnapshot(data, offset, group.Value);
        return data;
    }

    /// <summary>
    /// A message with a single player (the common case: a client receiving its own state) and optionally the group.
    /// </summary>
    public static byte[] EncodeOne(uint playerId, in OIDDAStateSnapshot player, OIDDAStateSnapshot? group)
    {
        var data = new byte[HeaderBytes + PlayerBytes + (group.HasValue ? SnapshotBytes : 0)];
        int offset = WriteHeader(data, 1, group.HasValue);

        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), playerId);
        offset = WriteSnapshot(data, offset + 4, player);

        if (group.HasValue) WriteSnapshot(data, offset, group.Value);
        return data;
    }

    /// <summary>
    /// A message with no players, only the group's state.
    /// </summary>
    public static byte[] EncodeGroupOnly(in OIDDAStateSnapshot group)
    {
        var data = new byte[HeaderBytes + SnapshotBytes];
        int offset = WriteHeader(data, 0, true);
        WriteSnapshot(data, offset, group);
        return data;
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

        int expected = HeaderBytes + count * PlayerBytes + (hasGroup ? SnapshotBytes : 0);
        if (data.Length != expected) return false;

        int offset = HeaderBytes;
        for (int i = 0; i < count; i++)
        {
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));
            offset += 4;

            if (!TryReadSnapshot(data, ref offset, out var snapshot))
            {
                players.Clear();
                return false;
            }
            players[id] = snapshot;
        }

        if (hasGroup)
        {
            if (!TryReadSnapshot(data, ref offset, out var groupSnapshot))
            {
                players.Clear();
                return false;
            }
            group = groupSnapshot;
        }

        return true;
    }

    static int WriteHeader(byte[] data, int count, bool hasGroup)
    {
        data[0] = Version;
        data[1] = hasGroup ? GroupFlag : (byte)0;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2), (ushort)count);
        return HeaderBytes;
    }

    static int WriteSnapshot(byte[] data, int offset, in OIDDAStateSnapshot s)
    {
        data[offset] = (byte)s.State;
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset + 1), s.Intensity);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset + 5), s.Stress);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset + 9), s.Fatigue);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset + 13), s.Score);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset + 17), s.DifficultyMultiplier);
        data[offset + 21] = s.ShouldSpawnEncounter ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 22), (ushort)Math.Clamp(s.PlayerCount, 0, ushort.MaxValue));
        return offset + SnapshotBytes;
    }

    static bool TryReadSnapshot(byte[] data, ref int offset, out OIDDAStateSnapshot snapshot)
    {
        snapshot = default;

        byte state = data[offset];
        if (!Enum.IsDefined(typeof(DirectorState), (int)state)) return false;

        byte flags = data[offset + 21];
        if ((flags & ~1) != 0) return false;

        snapshot = new OIDDAStateSnapshot
        {
            State = (DirectorState)state,
            Intensity = Sanitize(BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset + 1)), 0f, 100f),
            Stress = Sanitize(BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset + 5)), 0f, 100f),
            Fatigue = Sanitize(BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset + 9)), 0f, 100f),
            Score = Sanitize(BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset + 13)), 0f, 1f),
            DifficultyMultiplier = Sanitize(BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset + 17)), 0f, 10f),
            ShouldSpawnEncounter = (flags & 1) != 0,
            PlayerCount = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 22))
        };

        offset += SnapshotBytes;
        return true;
    }

    // NaN and infinity become 0; everything else is kept inside the range the value can legitimately have.
    static float Sanitize(float value, float min, float max) => float.IsFinite(value) ? Math.Clamp(value, min, max) : 0f;
}
