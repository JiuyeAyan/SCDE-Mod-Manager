using SHCDESE.API.Components.Network;
using SHCDESE.EventAPI.Network;
using Steamworks;
using System;
using SHCDESE.Logging;

namespace SHCDESE.EventAPI;

/// <summary>
/// This is the R3-style event object that mods will subscribe to.
/// It also contains the logic for deserializing and raising the event.
/// </summary>
/// <typeparam name="T">The type of the packet payload.</typeparam>
public class R3PacketEventHook<T> : IPacketR3Handler
{
    private readonly Int16 _packetId;
    private readonly R3EventHook<ReceiveCustomPacketEventArgs<T>> _r3EventHook = new();

    public R3PacketEventHook(Int16 packetId)
    {
        _packetId = packetId;
    }

    public Int16 GetPacketId()
    {
        return _packetId;
    }

    // Internal method to handle deserialization and raise the event.
    void IPacketR3Handler.HandleRaw(byte[] data, CSteamID? sender)
    {
        try
        {
            T packet = MessagePack.MessagePackSerializer.Deserialize<T>(data);
            _r3EventHook.Raise(new ReceiveCustomPacketEventArgs<T>(EventHookPhase.Post, _packetId, packet, sender));
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error while handling raw packet");
        }
    }

    // This is needed for the Lua bridge to work correctly
    public R3EventHook<ReceiveCustomPacketEventArgs<T>> GetBaseHook()
    {
        return _r3EventHook;
    }
}