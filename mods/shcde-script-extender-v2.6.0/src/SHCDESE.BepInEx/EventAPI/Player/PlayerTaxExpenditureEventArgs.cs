namespace SHCDESE.EventAPI.Player;

public class PlayerTaxExpenditureEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerId { get; }
    public int Expenditure { get; set; }

    public PlayerTaxExpenditureEventArgs(EventHookPhase phase, int playerId, int expenditure)
    {
        Phase = phase;
        PlayerId = playerId;
        Expenditure = expenditure;
    }
}
