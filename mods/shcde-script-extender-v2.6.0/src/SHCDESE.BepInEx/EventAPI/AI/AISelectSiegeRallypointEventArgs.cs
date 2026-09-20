namespace SHCDESE.EventAPI.AI;

public class AISelectSiegeRallypointEventArgs : EventHookBase
{
    public int MaxSearchRange { get; set; }
    public int PreferredStandoffDistance { get; set; }
    public int PathConnectionLayer { get; set; }
    public int PlayerId { get; }

    public AISelectSiegeRallypointEventArgs(EventHookPhase phase, int maxSearchRange, int preferredStandoffDistance, int pathConnectionLayer, int playerId)
    {
        Phase = phase;
        MaxSearchRange = maxSearchRange;
        PreferredStandoffDistance = preferredStandoffDistance;
        PathConnectionLayer = pathConnectionLayer;
        PlayerId = playerId;
    }
}
