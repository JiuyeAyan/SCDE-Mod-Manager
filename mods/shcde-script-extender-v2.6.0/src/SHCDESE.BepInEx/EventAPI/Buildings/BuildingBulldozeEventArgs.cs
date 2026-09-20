using System;

namespace SHCDESE.EventAPI.Buildings;

public class BuildingBulldozeEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int BuildingId { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; } = 0;

    public BuildingBulldozeEventArgs(EventHookPhase phase, int buildingId)
    {
        Phase = phase;
        BuildingId = buildingId;
    }
}
