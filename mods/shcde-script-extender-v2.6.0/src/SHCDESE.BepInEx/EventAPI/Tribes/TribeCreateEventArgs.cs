using System;

namespace SHCDESE.EventAPI.Tribes;

public class TribeCreateEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerIdOwner { get; set; }
    public int Unknown { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public TribeCreateEventArgs(EventHookPhase phase, int playerIdOwner, int bUnknown)
    {
        Phase = phase;
        PlayerIdOwner = playerIdOwner;
        Unknown = bUnknown;
    }
}