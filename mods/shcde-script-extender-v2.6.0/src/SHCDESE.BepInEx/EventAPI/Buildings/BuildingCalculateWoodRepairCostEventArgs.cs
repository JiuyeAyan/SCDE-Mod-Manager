using System;

namespace SHCDESE.EventAPI.Buildings;

public class BuildingCalculateWoodRepairCostEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int BuildingId { get; set; }
    public int RepairCost { get; set; }

    public BuildingCalculateWoodRepairCostEventArgs(EventHookPhase phase, int buildingId, int repairCost)
    {
        Phase = phase;
        BuildingId = buildingId;
        RepairCost = repairCost;
    }
}
