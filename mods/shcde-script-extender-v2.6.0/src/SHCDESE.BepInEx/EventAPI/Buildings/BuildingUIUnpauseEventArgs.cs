namespace SHCDESE.EventAPI.Buildings;

public class BuildingUIUnpauseEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---

    public BuildingUIUnpauseEventArgs(EventHookPhase phase)
    {
        Phase = phase;
    }
}
