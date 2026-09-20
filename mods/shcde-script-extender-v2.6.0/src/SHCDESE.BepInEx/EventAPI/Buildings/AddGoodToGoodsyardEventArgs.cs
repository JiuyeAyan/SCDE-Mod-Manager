using SHCDESE.Interop;
using System;

namespace SHCDESE.EventAPI.Buildings;

public class AddGoodToGoodsyardEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int BuildingId { get; set; }
    public int BuildingGlobalId { get; set; }
    public eGoods Good { get; set; }
    public int AddAmount { get; set; }
    public int Capacity { get; set; }
    public bool Add { get; set; }
    // --- Return Value ---
    public Int64 ReturnValue { get; } = 0;

    public AddGoodToGoodsyardEventArgs(EventHookPhase phase, int buildingId, int buildingGlobalId, eGoods good, int addAmont, int capacity, bool add)
    {
        Phase = phase;
        BuildingId = buildingId;
        BuildingGlobalId = buildingGlobalId;
        Good = good;
        AddAmount = addAmont;
        Capacity = capacity;
        Add = add;
    }
}
