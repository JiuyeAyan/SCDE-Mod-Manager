namespace SHCDESE.EventAPI.Player;

public class PlayerCalculateDrunkPercentageEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerId { get; set; }

    // --- Return Value ---
    public int ReturnValue { get; set; }

    public PlayerCalculateDrunkPercentageEventArgs(EventHookPhase phase, int playerId)
    {
        Phase = phase;
        PlayerId = playerId;
    }
}
