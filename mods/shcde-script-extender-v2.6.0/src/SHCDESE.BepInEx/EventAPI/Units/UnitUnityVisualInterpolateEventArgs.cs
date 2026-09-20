using System;
using UnityEngine;

namespace SHCDESE.EventAPI.Units;

public class UnitUnityVisualInterpolateEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Chimp Chimp { get; set; }

    public UnitUnityVisualInterpolateEventArgs(EventHookPhase phase, Chimp chimp)
    {
        Phase = phase;
        Chimp = chimp;
    }
}
