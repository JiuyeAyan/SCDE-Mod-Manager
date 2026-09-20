namespace SHCDESE.EventAPI.Buildings;

public class BuildingUIPauseEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---

    public BuildingUIPauseEventArgs(EventHookPhase phase)
    {
        Phase = phase;
    }
}
