using System.Collections.Generic;

namespace OIDDA.Network;

/// <summary>
/// A state message after decoding, whatever its version: the players that were in it, the group each one is in, and the groups.
/// Reused from one message to the next.
/// </summary>
public sealed class OIDDAStateFrame
{
    public readonly Dictionary<uint, OIDDAStateSnapshot> Players = new();

    /// <summary> Group id of each player in <see cref="Players"/>. </summary>
    public readonly Dictionary<uint, uint> PlayerGroups = new();

    public readonly Dictionary<uint, OIDDAStateSnapshot> Groups = new();

    public ushort Sequence;

    /// <summary> Version of the message the frame came from (0 when empty). </summary>
    public byte Version;

    public void Clear()
    {
        Players.Clear();
        PlayerGroups.Clear();
        Groups.Clear();
        Sequence = 0;
        Version = 0;
    }
}
