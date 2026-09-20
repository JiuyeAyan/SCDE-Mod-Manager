using System;

namespace SHCDESE.EventAPI.Tribes;

public class TribeGetNextPatrolWaypointEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int TribeId { get; }
    public int PatrolPointIndex { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public TribeGetNextPatrolWaypointEventArgs(EventHookPhase phase, int tribeId, int patrolPointIndex)
    {
        Phase = phase;
        TribeId = tribeId;
        PatrolPointIndex = patrolPointIndex;
    }
}
