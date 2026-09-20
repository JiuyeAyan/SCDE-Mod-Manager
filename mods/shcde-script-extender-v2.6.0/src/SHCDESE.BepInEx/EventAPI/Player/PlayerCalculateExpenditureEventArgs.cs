using SHCDESE.Interop.Enums;

namespace SHCDESE.EventAPI.Player;

public class PlayerCalculateExpenditureEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerId { get; set; }
    public TaxesMode TaxesMode { get; set; }
    public int Population { get; set; }

    // --- Return Value ---
    public int ReturnValue { get; set; }

    public PlayerCalculateExpenditureEventArgs(EventHookPhase phase, int playerId, TaxesMode taxesMode, int population)
    {
        Phase = phase;
        PlayerId = playerId;
        TaxesMode = taxesMode;
        Population = population;
    }
}
