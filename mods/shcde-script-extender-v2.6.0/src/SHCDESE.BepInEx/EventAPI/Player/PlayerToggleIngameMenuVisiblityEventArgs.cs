namespace SHCDESE.EventAPI.Player;

public class PlayerToggleIngameMenuVisibilityEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public bool State { get; set; }

    public PlayerToggleIngameMenuVisibilityEventArgs(EventHookPhase phase, bool state)
    {
        Phase = phase;
        State = state;
    }
}
