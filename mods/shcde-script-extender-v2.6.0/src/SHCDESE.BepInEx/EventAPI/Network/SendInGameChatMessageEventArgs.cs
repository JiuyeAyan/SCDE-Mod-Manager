using System.Collections.Generic;

namespace SHCDESE.EventAPI.Network;

public class SendInGameChatMessageEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public List<int> Recipients { get; }
    public string Message { get; }

    public SendInGameChatMessageEventArgs(EventHookPhase phase, List<int> recipients, string message)
    {
        Phase = phase;
        Recipients = recipients;
        Message = message;
    }
}
