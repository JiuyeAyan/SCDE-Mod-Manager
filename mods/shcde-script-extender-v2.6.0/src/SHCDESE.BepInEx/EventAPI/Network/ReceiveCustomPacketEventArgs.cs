using Steamworks;

namespace SHCDESE.EventAPI.Network;

/// <summary>
/// A strongly-typed container for a received custom packet. This is the object
/// passed to subscribers of a specific packet event.
/// </summary>
/// <typeparam name="T">The type of the packet payload.</typeparam>
public class ReceiveCustomPacketEventArgs<T> : AbstractReceiveCustomPacketEventArgs
{
    /// <summary>
    /// The deserialized packet object.
    /// </summary>
    public T Packet { get; }

    /// <summary>
    /// Steam ID of the peer that actually sent this packet, as reported by the transport rather
    /// than by any field inside the payload. <c>null</c> when the receive path could not
    /// establish it (currently the in-game <c>processMessage</c> route).
    /// </summary>
    /// <remarks>
    /// Unlike payload fields, this value is not attacker-controlled, so it is the only safe
    /// basis for authorisation decisions such as "did this really come from the host?".
    /// </remarks>
    public CSteamID? SenderSteamId { get; }

    public ReceiveCustomPacketEventArgs(EventHookPhase phase, short packetId, T packet, CSteamID? senderSteamId = null) : base(phase, packetId)
    {
        Packet = packet;
        SenderSteamId = senderSteamId;
    }
}