using System;

namespace SHCDESE.EventAPI.Buildings;

public class BuildingRepairEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerId { get; }
    public int BuildingId { get; }
    public int WoodCost { get; set; }
    public int StoneCost { get; set; }
    public int BuildingGlobalId { get; }

    public BuildingRepairEventArgs(EventHookPhase phase, int playerId, int buildingId, int woodCost, int stoneCost, int buildingGlobalId)
    {
        Phase = phase;
        PlayerId = playerId;
        BuildingId = buildingId;
        WoodCost = woodCost;
        StoneCost = stoneCost;
        BuildingGlobalId = buildingGlobalId;
    }
}
