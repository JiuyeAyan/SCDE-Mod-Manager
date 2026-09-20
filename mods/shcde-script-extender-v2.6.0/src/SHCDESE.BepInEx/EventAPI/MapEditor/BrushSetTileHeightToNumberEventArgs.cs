using System;

namespace SHCDESE.EventAPI.MapEditor;

public class BrushSetTileHeightToNumberEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int CenterTileId { get; set; }
    public int CenterTileY { get; set; }
    public int BrushSize { get; set; }
    public Int16 Height { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public BrushSetTileHeightToNumberEventArgs(EventHookPhase phase, int centerTileId, int centerTileY, int brushSize, Int16 height)
    {
        Phase = phase;
        CenterTileId = centerTileId;
        CenterTileY = centerTileY;
        BrushSize = brushSize;
        Height = height;
    }
}

