using SHCDESE.Interop;

namespace SHCDESE.EventAPI.Player;

public class PlayerAIRequestGoodsEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int SourcePlayerId { get; set; }
    public int TargetPlayerId { get; set; }
    public eGoods Good { get; set; }
    public int Amount { get; set; }

    public PlayerAIRequestGoodsEventArgs(EventHookPhase phase, int sourcePlayerId, int targetPlayerId, eGoods good, int amount)
    {
        Phase = phase;
        SourcePlayerId = sourcePlayerId;
        TargetPlayerId = targetPlayerId;
        Good = good;
        Amount = amount;
    }
}
