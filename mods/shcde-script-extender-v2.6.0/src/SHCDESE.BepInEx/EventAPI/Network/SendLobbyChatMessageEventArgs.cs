namespace SHCDESE.EventAPI.Network;

public class SendLobbyChatMessageEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public string Message { get; }

    public SendLobbyChatMessageEventArgs(EventHookPhase phase, string message)
    {
        Phase = phase;
        Message = message;
    }
}
