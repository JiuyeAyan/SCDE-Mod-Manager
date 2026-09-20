using MessagePack;
using System.Collections.Generic;

namespace SHCDESE.API.Components.Network;

/// <summary>
/// Represents the data structure for a Remote Procedure Call (RPC)
/// intended to be executed within the Lua environment.
/// </summary>
[MessagePackObject(true)]
public class LuaNetworkRPCPacket
{
    /// <summary>
    /// The id of the player who sent this packet.
    /// </summary>
    public int FromPlayerId;

    /// <summary>
    /// The name of the global Lua function to be called on the receiving client.
    /// </summary>
    public string FunctionName;

    /// <summary>
    /// An optional dictionary of arguments to be passed to the Lua function.
    /// This will be converted to a Lua table.
    /// </summary>
    public Dictionary<string, string>? FunctionArgsTable;
}