namespace SHCDESE.EventAPI.Player;

public class PlayerAIEvaluateDefendOrder : EventHookBase
{
    // --- Input/Output Parameters ---
    public int TargetPlayerId { get; set; }
    public int SourcePlayerId { get; set; }

    // --- Return Value ---
    public bool ReturnValue { get; set; }

    public PlayerAIEvaluateDefendOrder(EventHookPhase phase, int targetPlayerId, int sourcePlayerId)
    {
        Phase = phase;
        TargetPlayerId = targetPlayerId;
        SourcePlayerId = sourcePlayerId;
    }
}
