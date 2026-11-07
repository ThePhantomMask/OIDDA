using OIDDA.Data;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace OIDDA.Network;

/// <summary>
/// Compact binary format of the state a server sends to a client: some players' snapshots and the state of the group (version 2:
/// group 0 only, with full 32-bit numbers) or of every group (version 3, which also tells which group each player is in and
/// stores the snapshot numbers as 16-bit values, about half the size).
/// A client decodes both versions (<see cref="TryDecode(byte[], OIDDAStateFrame)"/>); the server chooses which one to send.
/// Decoding checks everything, so a malformed message is rejected instead of corrupting the client's state.
/// Every message is written straight into one array of exactly the right size.
/// Each message carries a sequence number, so a receiver can drop one that arrives after a newer one (needed on an unreliable channel).
/// </summary>
public static class OIDDANetworkCodec
{
    /// <summary> The format of earlier versions: players and group 0 only. </summary>
    public const byte Version2 = 2;

    /// <summary> Every player carries its group id, and the state of every group is sent. </summary>
    public const byte Version3 = 3;

    public const int MaxPlayersPerMessage = 256;
    public const int MaxGroupsPerMessage = 255;

    const byte GroupFlag = 1;
    const int HeaderBytes = 1 + 1 + 2 + 2;
    const int SnapshotBytes = 1 + 5 * 4 + 1 + 2;
    const int PlayerBytes = 4 + SnapshotBytes;

    // Version 3: the header has one more byte (the number of groups), a player has its group id (4) and a group has its own id (4).
    // Its snapshot is compact: the five numbers are 16-bit values scaled to the range each one can have.
    const int HeaderBytes3 = HeaderBytes + 1;
    const int SnapshotBytes3 = 1 + 5 * 2 + 1 + 2;
    const int PlayerBytes3 = 4 + 4 + SnapshotBytes3;
    const int GroupBytes3 = 4 + SnapshotBytes3;

    // Ranges of the snapshot numbers (the same ones decoding clamps to).
    const float MaxPercent = 100f;
    const float MaxScore = 1f;
    const float MaxMultiplier = 10f;

    /// <summary>
    /// True if <paramref name="sequence"/> comes after <paramref name="last"/>. The numbers wrap around: anything up to half the range
    /// ahead counts as newer, an equal or older number does not.
    /// </summary>
    public static bool IsNewer(ushort sequence, ushort last)
    {
        int ahead = (ushort)(sequence - last);
        return ahead > 0 && ahead < 0x8000;
    }

    public static byte[] Encode(IReadOnlyList<KeyValuePair<uint, OIDDAStateSnapshot>> players, OIDDAStateSnapshot? group, ushort sequence = 0)
    {
        int count = Math.Min(players.Count, MaxPlayersPerMessage);
        var data = new byte[HeaderBytes + count * PlayerBytes + (group.HasValue ? SnapshotBytes : 0)];
        int offset = WriteHeader(data, count, group.HasValue, sequence);

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
    public static byte[] EncodeOne(uint playerId, in OIDDAStateSnapshot player, OIDDAStateSnapshot? group, ushort sequence = 0)
    {
        var data = new byte[HeaderBytes + PlayerBytes + (group.HasValue ? SnapshotBytes : 0)];
        int offset = WriteHeader(data, 1, group.HasValue, sequence);

        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), playerId);
        offset = WriteSnapshot(data, offset + 4, player);

        if (group.HasValue) WriteSnapshot(data, offset, group.Value);
        return data;
    }

    /// <summary>
    /// A message with no players, only the group's state.
    /// </summary>
    public static byte[] EncodeGroupOnly(in OIDDAStateSnapshot group, ushort sequence = 0)
    {
        var data = new byte[HeaderBytes + SnapshotBytes];
        int offset = WriteHeader(data, 0, true, sequence);
        WriteSnapshot(data, offset, group);
        return data;
    }

    /// <summary>
    /// Decodes a message. 
    /// On failure the output is empty and false is returned.
    /// </summary>
    public static bool TryDecode(byte[] data, Dictionary<uint, OIDDAStateSnapshot> players, out OIDDAStateSnapshot? group)
        => TryDecode(data, players, out group, out _);

    /// <summary>
    /// Same as the other overload, and also gives the message's sequence number.
    /// </summary>
    public static bool TryDecode(byte[] data, Dictionary<uint, OIDDAStateSnapshot> players, out OIDDAStateSnapshot? group, out ushort sequence)
    {
        players.Clear();
        group = null;
        sequence = 0;

        if (data == null || data.Length < HeaderBytes || data[0] != Version2) return false;

        byte flags = data[1];
        if ((flags & ~GroupFlag) != 0) return false;

        bool hasGroup = (flags & GroupFlag) != 0;
        int count = data[2] | (data[3] << 8);
        if (count > MaxPlayersPerMessage) return false;
        ushort messageSequence = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4));

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

        sequence = messageSequence;
        return true;
    }

    /// <summary>
    /// Version 3: the players (each with the id of its group) and the state of every group.
    /// </summary>
    public static byte[] EncodeV3(IReadOnlyList<(uint Id, uint GroupId, OIDDAStateSnapshot State)> players, IReadOnlyList<KeyValuePair<uint, OIDDAStateSnapshot>> groups, ushort sequence = 0)
    {
        int playerCount = Math.Min(players.Count, MaxPlayersPerMessage);
        int groupCount = Math.Min(groups.Count, MaxGroupsPerMessage);
        var data = new byte[HeaderBytes3 + playerCount * PlayerBytes3 + groupCount * GroupBytes3];

        data[0] = Version3;
        data[1] = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2), (ushort)playerCount);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4), sequence);
        data[6] = (byte)groupCount;

        int offset = HeaderBytes3;
        for (int i = 0; i < playerCount; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), players[i].Id);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 4), players[i].GroupId);
            offset = WriteSnapshot3(data, offset + 8, players[i].State);
        }

        for (int i = 0; i < groupCount; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), groups[i].Key);
            offset = WriteSnapshot3(data, offset + 4, groups[i].Value);
        }
        return data;
    }

    /// <summary>
    /// Decodes a message of either version into <paramref name="frame"/> (which is cleared first). With version 2 every player is in
    /// group 0 and the only group is group 0. On failure the frame is empty and false is returned.
    /// </summary>
    public static bool TryDecode(byte[] data, OIDDAStateFrame frame)
    {
        frame.Clear();
        if (data == null || data.Length < 1) return false;

        bool ok;
        switch (data[0])
        {
            case Version2:
                ok = TryDecode(data, frame.Players, out var group, out var sequence);
                if (ok)
                {
                    foreach (var id in frame.Players.Keys) frame.PlayerGroups[id] = 0;
                    if (group.HasValue) frame.Groups[0] = group.Value;
                    frame.Sequence = sequence;
                }
                break;
            case Version3:
                ok = TryDecodeV3(data, frame);
                break;
            default:
                return false;
        }

        if (!ok)
        {
            frame.Clear();
            return false;
        }

        frame.Version = data[0];
        return true;
    }

    static bool TryDecodeV3(byte[] data, OIDDAStateFrame frame)
    {
        if (data.Length < HeaderBytes3 || data[1] != 0) return false;

        int playerCount = data[2] | (data[3] << 8);
        if (playerCount > MaxPlayersPerMessage) return false;

        int groupCount = data[6];
        if (data.Length != HeaderBytes3 + playerCount * PlayerBytes3 + groupCount * GroupBytes3) return false;

        int offset = HeaderBytes3;
        for (int i = 0; i < playerCount; i++)
        {
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));
            uint groupId = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 4));
            offset += 8;

            if (!TryReadSnapshot3(data, ref offset, out var snapshot)) return false;
            frame.Players[id] = snapshot;
            frame.PlayerGroups[id] = groupId;
        }

        for (int i = 0; i < groupCount; i++)
        {
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));
            offset += 4;

            if (!TryReadSnapshot3(data, ref offset, out var snapshot)) return false;
            frame.Groups[id] = snapshot;
        }

        frame.Sequence = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4));
        return true;
    }

    static int WriteHeader(byte[] data, int count, bool hasGroup, ushort sequence)
    {
        data[0] = Version2;
        data[1] = hasGroup ? GroupFlag : (byte)0;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2), (ushort)count);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4), sequence);
        return HeaderBytes;
    }

    // Compact snapshot (version 3): state (1), intensity, stress, fatigue, score, difficulty multiplier (5 x 16 bits), flags (1), player count (2).
    static int WriteSnapshot3(byte[] data, int offset, in OIDDAStateSnapshot s)
    {
        data[offset] = (byte)s.State;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 1), Quantize(s.Intensity, MaxPercent));
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 3), Quantize(s.Stress, MaxPercent));
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 5), Quantize(s.Fatigue, MaxPercent));
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 7), Quantize(s.Score, MaxScore));
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 9), Quantize(s.DifficultyMultiplier, MaxMultiplier));
        data[offset + 11] = s.ShouldSpawnEncounter ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 12), (ushort)Math.Clamp(s.PlayerCount, 0, ushort.MaxValue));
        return offset + SnapshotBytes3;
    }

    static bool TryReadSnapshot3(byte[] data, ref int offset, out OIDDAStateSnapshot snapshot)
    {
        snapshot = default;

        byte state = data[offset];
        if (!Enum.IsDefined(typeof(DirectorState), (int)state)) return false;

        byte flags = data[offset + 11];
        if ((flags & ~1) != 0) return false;

        snapshot = new OIDDAStateSnapshot
        {
            State = (DirectorState)state,
            Intensity = Dequantize(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 1)), MaxPercent),
            Stress = Dequantize(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 3)), MaxPercent),
            Fatigue = Dequantize(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 5)), MaxPercent),
            Score = Dequantize(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 7)), MaxScore),
            DifficultyMultiplier = Dequantize(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 9)), MaxMultiplier),
            ShouldSpawnEncounter = (flags & 1) != 0,
            PlayerCount = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 12))
        };

        offset += SnapshotBytes3;
        return true;
    }

    // A number in 0..max as a 16-bit value: the step is max / 65535 (0.0015 for a percentage), so the error is at most half of it.
    // NaN and infinity become 0, like in version 2; anything outside the range is clamped to it.
    static ushort Quantize(float value, float max) => float.IsFinite(value) ? (ushort)MathF.Round(Math.Clamp(value, 0f, max) / max * ushort.MaxValue) : (ushort)0;

    static float Dequantize(ushort quantized, float max) => quantized * max / ushort.MaxValue;

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
