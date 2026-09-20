using System;

namespace SHCDESE.EventAPI.MapLoader;

public class MapUnloadEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public IntPtr TileManager { get; }

    // --- Return Value ---
    public UInt64 ReturnValue { get; set; } = 0;

    public MapUnloadEventArgs(EventHookPhase phase, IntPtr tileManager)
    {
        Phase = phase;
        TileManager = tileManager;
    }
}