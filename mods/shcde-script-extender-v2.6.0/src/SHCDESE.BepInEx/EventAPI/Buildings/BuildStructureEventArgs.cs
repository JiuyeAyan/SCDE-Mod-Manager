using SHCDESE.Interop;
using System;

namespace SHCDESE.EventAPI.Buildings;

public class BuildStructureEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerId { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public eMappers Mappers { get; set; }
    public int BuildingScaleUnknown { get; set; }
    public int Unknown1 { get; set; }
    public bool IsFree { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; } = 0;

    public BuildStructureEventArgs(EventHookPhase phase, int playerId, int tileX, int tileY, eMappers mv, int buildingScaleUnknown, int unknown1, bool bIsFree)
    {
        Phase = phase;
        PlayerId = playerId;
        TileX = tileX;
        TileY = tileY;
        Mappers = mv;
        BuildingScaleUnknown = buildingScaleUnknown;
        Unknown1 = unknown1;
        IsFree = bIsFree;
    }
}
