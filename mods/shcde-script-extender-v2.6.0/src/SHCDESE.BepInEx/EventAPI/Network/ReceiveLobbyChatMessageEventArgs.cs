using Steamworks;

namespace SHCDESE.EventAPI.Network;

public class ReceiveLobbyChatMessageEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public string Message { get; }
    public CSteamID SteamId { get; }

    public ReceiveLobbyChatMessageEventArgs(EventHookPhase phase, string message, CSteamID steamId)
    {
        Phase = phase;
        Message = message;
        SteamId = steamId;
    }
}
