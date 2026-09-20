using System;

namespace SHCDESE.EventAPI.Buildings;

public class BuildingCalculateStoneRepairCostEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int BuildingId { get; set; }
    public int RepairCost { get; set; }

    public BuildingCalculateStoneRepairCostEventArgs(EventHookPhase phase, int buildingId, int repairCost)
    {
        Phase = phase;
        BuildingId = buildingId;
        RepairCost = repairCost;
    }
}
