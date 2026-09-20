using SHCDESE.Interop;

namespace SHCDESE.EventAPI.Player;

public class PlayerSubtractResourceEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerId { get; set; }
    public eGoods Good { get; set; }
    public int Amount { get; set; }
    public bool DontSubtract { get; set; }

    public PlayerSubtractResourceEventArgs(EventHookPhase phase, int playerId, eGoods good, int amount, bool bDontSubtract)
    {
        Phase = phase;
        PlayerId = playerId;
        Good = good;
        Amount = amount;
        DontSubtract = bDontSubtract;
    }
}
