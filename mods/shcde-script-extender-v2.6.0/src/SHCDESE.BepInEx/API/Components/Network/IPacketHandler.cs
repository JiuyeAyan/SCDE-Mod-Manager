using SHCDESE.EventAPI;
using Steamworks;

namespace SHCDESE.API.Components.Network;

/// <summary>
/// Provides a non-generic interface for internal packet handlers.
/// This allows different generic <see cref="R3PacketEventHook{T}"/> instances
/// to be stored in a single collection within the <see cref="GameNetworkAPI"/>.
/// </summary>
public interface IPacketR3Handler
{
    /// <summary>
    /// Handles a raw byte array representing a packet's data.
    /// The implementation is responsible for deserializing the data into the correct type
    /// and raising the corresponding R3 event.
    /// </summary>
    /// <param name="data">The raw packet data.</param>
    /// <param name="sender">
    /// Steam ID of the peer the packet actually arrived from, as reported by the transport.
    /// <c>null</c> when the receive path cannot establish it. Handlers that make trust decisions
    /// must treat <c>null</c> as untrusted rather than assuming a sender.
    /// </param>
    void HandleRaw(byte[] data, CSteamID? sender);
}