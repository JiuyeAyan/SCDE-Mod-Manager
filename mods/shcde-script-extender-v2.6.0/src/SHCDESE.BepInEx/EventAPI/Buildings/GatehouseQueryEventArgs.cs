namespace SHCDESE.EventAPI.Buildings;
public class GatehouseQueryEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int UnitId { get; }
    public int BuildingId { get; }

    // --- Return Value ---
    public bool? ShouldClose { get; set; } = null!;

    public GatehouseQueryEventArgs(EventHookPhase phase, int unitId, int buildingId)
    {
        Phase = phase;
        UnitId = unitId;
        BuildingId = buildingId;
    }
}
