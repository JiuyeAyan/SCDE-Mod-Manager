namespace SHCDESE.EventAPI.Player;

public class PlayerAIEvaluateAttackOrder : EventHookBase
{
    // --- Input/Output Parameters ---
    public int TargetPlayerId { get; set; }
    public int SourcePlayerId { get; set; }
    public int ContextPlayerId { get; set; }

    // --- Return Value ---
    public bool ReturnValue { get; set; }

    public PlayerAIEvaluateAttackOrder(EventHookPhase phase, int targetPlayerId, int sourcePlayerId, int contextPlayerId)
    {
        Phase = phase;
        TargetPlayerId = targetPlayerId;
        SourcePlayerId = sourcePlayerId;
        ContextPlayerId = contextPlayerId;
    }
}
