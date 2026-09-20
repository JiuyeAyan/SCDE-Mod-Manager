using SHCDESE.Interop;
using System;

namespace SHCDESE.EventAPI.MapEditor;

public class AnimalPlaceEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public eMappers MappersValue { get; set; }
    public int WorldTileX { get; set; }
    public int WorldTileY { get; set; }
    public UInt16 HeightElevation { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public AnimalPlaceEventArgs(EventHookPhase phase, eMappers mappersValue, int worldTileX, int worldTileY, UInt16 heightElevation)
    {
        Phase = phase;
        MappersValue = mappersValue;
        WorldTileX = worldTileX;
        WorldTileY = worldTileY;
        HeightElevation = heightElevation;
    }
}

