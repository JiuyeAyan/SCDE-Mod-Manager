namespace SHCDESE.EventAPI.Tribes;

public class TribeDeleteEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int TribeId { get; set; }

    public TribeDeleteEventArgs(EventHookPhase phase, int tribeId)
    {
        Phase = phase;
        TribeId = tribeId;
    }
}

