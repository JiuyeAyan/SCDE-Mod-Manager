using SHCDESE.Interop;

namespace SHCDESE.EventAPI.Player;

public class PlayerAddResourceEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerId { get; set; }
    public eGoods Good { get; set; }
    public int Amount { get; set; }

    // --- Return Value ---
    public bool ReturnValue { get; set; }

    public PlayerAddResourceEventArgs(EventHookPhase phase, int playerId, eGoods good, int amount)
    {
        Phase = phase;
        PlayerId = playerId;
        Good = good;
        Amount = amount;
    }
}
