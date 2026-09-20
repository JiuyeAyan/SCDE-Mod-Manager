using System;

namespace SHCDESE.EventAPI.Units;
public class UnitUnityVisualRemoveEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Chimp Chimp { get; set; }

    public UnitUnityVisualRemoveEventArgs(EventHookPhase phase, Chimp chimp)
    {
        Phase = phase;
        Chimp = chimp;
    }
}
