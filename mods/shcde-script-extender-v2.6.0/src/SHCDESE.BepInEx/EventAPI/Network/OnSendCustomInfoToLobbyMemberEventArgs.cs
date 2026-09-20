using static Platform_Multiplayer;

namespace SHCDESE.EventAPI.Network;

public class OnSendCustomInfoToLobbyMemberEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public MPLobbyMember Member { get; }

    public OnSendCustomInfoToLobbyMemberEventArgs(EventHookPhase phase, MPLobbyMember member)
    {
        Phase = phase;
        Member = member;
    }
}
