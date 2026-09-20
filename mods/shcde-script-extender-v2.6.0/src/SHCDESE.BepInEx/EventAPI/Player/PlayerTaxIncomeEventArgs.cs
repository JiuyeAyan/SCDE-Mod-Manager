namespace SHCDESE.EventAPI.Player;

public class PlayerTaxIncomeEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerId { get; }
    public int Income { get; set; }

    public PlayerTaxIncomeEventArgs(EventHookPhase phase, int playerId, int income)
    {
        Phase = phase;
        PlayerId = playerId;
        Income = income;
    }
}
