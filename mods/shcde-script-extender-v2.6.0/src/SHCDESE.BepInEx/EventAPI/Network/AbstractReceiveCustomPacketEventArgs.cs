namespace SHCDESE.EventAPI.Network;

/// <summary>
/// A non-generic base class for custom packet event arguments. Can be used to subscribe
/// to an event that fires for any custom packet, regardless of its specific type.
/// </summary>
public abstract class AbstractReceiveCustomPacketEventArgs : EventHookBase
{
    /// <summary>
    /// The unique identifier of the received packet.
    /// </summary>
    public short PacketId { get; }

    protected AbstractReceiveCustomPacketEventArgs(EventHookPhase phase, short packetId)
    {
        Phase = phase;
        PacketId = packetId;
    }
}
