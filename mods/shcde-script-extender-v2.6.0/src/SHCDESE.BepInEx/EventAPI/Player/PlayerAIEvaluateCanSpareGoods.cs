using SHCDESE.Interop;

namespace SHCDESE.EventAPI.Player;

public class PlayerAIEvaluateCanSpareGoods : EventHookBase
{
    // --- Input/Output Parameters ---
    public int TargetPlayerId { get; set; }
    public eGoods Good { get; set; }
    public int Amount { get; set; }

    // --- Return Value ---
    public bool ReturnValue { get; set; }

    public PlayerAIEvaluateCanSpareGoods(EventHookPhase phase, int targetPlayerId, eGoods good, int amount)
    {
        Phase = phase;
        TargetPlayerId = targetPlayerId;
        Good = good;
        Amount = amount;
    }
}
