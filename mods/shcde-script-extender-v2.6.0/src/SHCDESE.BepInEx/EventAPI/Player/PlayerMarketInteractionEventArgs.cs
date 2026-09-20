using SHCDESE.Interop;

namespace SHCDESE.EventAPI.Player;

public class PlayerMarketInteractionEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerId { get; set; }
    public bool Selling { get; set; }
    public eGoods Good { get; set; }
    public int ShiftModifier { get; set; }

    public PlayerMarketInteractionEventArgs(EventHookPhase phase, int playerId, bool selling, eGoods good, int bShiftModifier)
    {
        Phase = phase;
        PlayerId = playerId;
        Selling = selling;
        Good = good;
        ShiftModifier = bShiftModifier;
    }
}
