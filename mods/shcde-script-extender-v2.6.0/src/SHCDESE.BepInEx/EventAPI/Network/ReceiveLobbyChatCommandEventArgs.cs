using Steamworks;

namespace SHCDESE.EventAPI.Network;

public class ReceiveLobbyChatCommandEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public string Command { get; }
    public string[] Arguments { get; }
    public CSteamID SteamId { get; }

    public ReceiveLobbyChatCommandEventArgs(EventHookPhase phase, string command, string[] arguments, CSteamID steamId)
    {
        Phase = phase;
        Command = command;
        Arguments = arguments;
        SteamId = steamId;
    }
}
