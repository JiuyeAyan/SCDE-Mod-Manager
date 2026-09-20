namespace SHCDESE.EventAPI.Network;

public class ReceiveInGameChatCommandEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public string Command { get; }
    public string[] Arguments { get; }
    public string FromPlayerName { get; }
    public int FromPlayerId { get; }

    public ReceiveInGameChatCommandEventArgs(EventHookPhase phase, string command, string[] arguments, string fromPlayerName, int fromPlayerId)
    {
        Phase = phase;
        Command = command;
        Arguments = arguments;
        FromPlayerName = fromPlayerName;
        FromPlayerId = fromPlayerId;
    }
}
