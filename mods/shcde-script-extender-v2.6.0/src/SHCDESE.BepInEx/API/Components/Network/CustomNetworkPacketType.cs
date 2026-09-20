namespace SHCDESE.API.Components.Network;

/// <summary>
/// Defines unique identifiers for custom network packets.
/// This enum uses a sequential numbering scheme starting from a high offset (<see cref="CustomPacketStart"/>)
/// to prevent conflicts with the game's built-in packet IDs.
/// Mod authors should define their own packet IDs by adding to this starting offset.
/// </summary>
public enum CustomNetworkPacketType : short
{
    // Note: The lower values are reserved and should not be used.

    /// <summary>
    /// The starting identifier for all custom packets. Any packet with an ID greater than or equal to this value
    /// will be intercepted by the custom network manager.
    /// </summary>
    CustomPacketStart = 1000,

    /// <summary>
    /// A packet containing a generic Lua table (serialized as a dictionary).
    /// </summary>
    LuaTable = CustomPacketStart + 1,   // ID = 1001

    /// <summary>
    /// A packet representing a Remote Procedure Call (RPC) intended for the Lua environment.
    /// </summary>
    LuaRPC = CustomPacketStart + 2,     // ID = 1002

    /// <summary>
    /// A packet that provides a shared seed for random events for all players.
    /// </summary>
    SeedSync = CustomPacketStart + 3,   // ID = 10023

}