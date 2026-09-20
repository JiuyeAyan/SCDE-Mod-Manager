namespace SHCDESE.EventAPI.Network;

public class ReceiveInGameChatMessageEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public string FromPlayerName { get; set; }
    public int FromPlayerId { get; set; }
    public string Message { get; }
    public int Duration { get; }

    public ReceiveInGameChatMessageEventArgs(EventHookPhase phase, string fromPlayerName, int fromPlayerId, string message, int duration)
    {
        Phase = phase;
        FromPlayerName = fromPlayerName;
        FromPlayerId = fromPlayerId;
        Message = message;
        Duration = duration;
    }
}
