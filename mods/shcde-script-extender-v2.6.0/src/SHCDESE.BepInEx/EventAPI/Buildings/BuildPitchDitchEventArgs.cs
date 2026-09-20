using System;

namespace SHCDESE.EventAPI.Buildings;

public class BuildPitchDitchEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerId { get; }
    public int TileX { get; }
    public int TileY { get; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public BuildPitchDitchEventArgs(EventHookPhase phase, int playerid, int tileX, int tileY)
    {
        Phase = phase;
        PlayerId = playerid; 
        TileX = tileX; 
        TileY = tileY;
    }
}
