using SHCDESE.Interop.Enums;
using System;

namespace SHCDESE.EventAPI.MapEditor;

public class BrushSetTileTypeEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int BrushSize { get; set; }
    public int CenterTileId { get; set; }
    public int CenterTileY { get; set; }
    public int Unknown1 { get; set; }
    public TilePropertyFlag TileProperty { get; set; }
    public TileType TileType { get; set; }
    public bool Unknown2 { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public BrushSetTileTypeEventArgs(EventHookPhase phase, int brushSize, int centerTileId, int centerTileY, int unknown1, TilePropertyFlag tileProperty, TileType tileType, bool unknown2)
    {
        Phase = phase;
        BrushSize = brushSize;
        Unknown1 = unknown1;
        CenterTileId = centerTileId;
        CenterTileY = centerTileY;
        TileProperty = tileProperty;
        TileType = tileType;
        Unknown2 = unknown2;
    }
}

