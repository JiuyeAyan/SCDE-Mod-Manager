using System;

namespace SHCDESE.EventAPI.Buildings;

public class RemovePitchDitchEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int TileId { get; }

    public RemovePitchDitchEventArgs(EventHookPhase phase, int tileId)
    {
        Phase = phase;
        TileId = tileId;
    }
}
