using SHCDESE.Interop;
using System;

namespace SHCDESE.EventAPI.MapEditor;

public class VegetationPlaceEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int TileX { get; set; }
    public int TileY { get; set; }
    public eMappers Mappers { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public VegetationPlaceEventArgs(EventHookPhase phase, int tileX, int tileY, eMappers eMappers)
    {
        Phase = phase;
        TileX = tileX;
        TileY = tileY;
        Mappers = eMappers;
    }
}

