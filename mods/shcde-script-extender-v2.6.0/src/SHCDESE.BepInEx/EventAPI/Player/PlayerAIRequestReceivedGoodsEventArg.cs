using SHCDESE.Interop;

namespace SHCDESE.EventAPI.Player;

public class PlayerAIRequestReceivedGoodsEventArg : EventHookBase
{
    // --- Input/Output Parameters ---
    public int SourcePlayerId { get; set; }
    public int TargetPlayerId { get; set; }
    public eGoods Good { get; set; }
    public int Amount { get; set; }

    // --- Return Value ---
    public bool ReturnValue { get; set; }

    public PlayerAIRequestReceivedGoodsEventArg(EventHookPhase phase, int sourcePlayerId, int targetPlayerId, eGoods good, int amount)
    {
        Phase = phase;
        SourcePlayerId = sourcePlayerId;
        TargetPlayerId = targetPlayerId;
        Good = good;
        Amount = amount;
    }
}
