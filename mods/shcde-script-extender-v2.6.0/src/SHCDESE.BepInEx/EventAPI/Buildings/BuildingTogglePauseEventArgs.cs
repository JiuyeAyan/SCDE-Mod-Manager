namespace SHCDESE.EventAPI.Buildings;

public class BuildingTogglePauseEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int BuildingId { get; }
    public bool WasPreviouslySleeping { get; }

    public BuildingTogglePauseEventArgs(EventHookPhase phase, int buildingId, bool wasPreviouslySleeping)
    {
        Phase = phase;
        BuildingId = buildingId;
        WasPreviouslySleeping = wasPreviouslySleeping;
    }
}
