namespace SHCDESE.EventAPI.Steamworks;

public class SteamworksInitializedEventArgs : EventHookBase
{
    public SteamworksInitializedEventArgs(EventHookPhase phase)
    {
        Phase = phase;
    }
}
