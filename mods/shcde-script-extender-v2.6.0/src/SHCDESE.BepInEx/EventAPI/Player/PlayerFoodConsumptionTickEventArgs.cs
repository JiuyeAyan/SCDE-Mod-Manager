namespace SHCDESE.EventAPI.Player;

public class PlayerFoodConsumptionTickEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerId { get; set; }

    public int FoodConsumptionThisTick { get; set; }

    public PlayerFoodConsumptionTickEventArgs(EventHookPhase phase, int playerId, int foodConsumptionThisTick)
    {
        Phase = phase;
        PlayerId = playerId;
        FoodConsumptionThisTick = foodConsumptionThisTick;
    }
}
