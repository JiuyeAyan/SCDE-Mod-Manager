using MessagePack;
using System.Collections.Generic;

namespace SHCDESE.API.Components.Network;

/// <summary>
/// Represents the data structure for sending a generic Lua table over the network.
/// The table is serialized as a dictionary of strings.
/// </summary>
[MessagePackObject(true)]
public class LuaNetworkTablePacket
{
    /// <summary>
    /// The id of the player who sent this packet.
    /// </summary>
    public int FromPlayerId;

    /// <summary>
    /// The dictionary containing the key-value pairs of the Lua table.
    /// </summary>
    public Dictionary<string, string> PacketTable;
}