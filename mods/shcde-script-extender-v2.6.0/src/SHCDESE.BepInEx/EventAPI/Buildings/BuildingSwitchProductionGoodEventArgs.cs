using SHCDESE.Interop;
using System;

namespace SHCDESE.EventAPI.Buildings;

public class BuildingSwitchProductionGoodEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int BuildingId { get; set; }
    public eGoods Good { get; set; }
    public int BuildingGlobalId { get; set; }
    public Int64 ReturnValue { get; } = 0;

    public BuildingSwitchProductionGoodEventArgs(EventHookPhase phase, int buildingId, eGoods good, int buildingGlobalId)
    {
        Phase = phase;
        BuildingId = buildingId;
        Good = good;
        BuildingGlobalId = buildingGlobalId;
    }
}
