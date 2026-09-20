using SHCDESE.Interop;
using System;

namespace SHCDESE.EventAPI.Buildings;

public class BuildWallEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerId { get; set; }
    public int TileXBegin { get; set; }
    public int TileYBegin { get; set; }
    public int TileXEnd { get; set; }
    public int TileYEnd { get; set; }
    public eMappers WallType { get; set; }
    public int BuildWallsMaximum { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; } = 0;

    public BuildWallEventArgs(EventHookPhase phase, int playerId, int tileXBegin, int tileYBegin, int tileXEnd, int tileYEnd, eMappers wallType, int buildWallsMaximum)
    {
        Phase = phase;
        PlayerId = playerId;
        TileXBegin = tileXBegin;
        TileYBegin = tileYBegin;
        TileXEnd = tileXEnd;
        TileYEnd = tileYEnd;
        WallType = wallType;
        BuildWallsMaximum = buildWallsMaximum;
    }
}
