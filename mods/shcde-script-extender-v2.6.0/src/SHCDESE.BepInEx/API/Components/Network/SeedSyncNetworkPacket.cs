using MessagePack;

namespace SHCDESE.API.Components.Network;

/// <summary>
/// Responsible for providing all players with a shared seed for random stuff.
/// </summary>
[MessagePackObject(true)]
public class SeedSyncNetworkPacket
{
    /// <summary>
    /// The id of the player who sent this packet.
    /// </summary>
    public int FromPlayerId;

    /// <summary>
    /// The seed
    /// </summary>
    public int Seed;
}