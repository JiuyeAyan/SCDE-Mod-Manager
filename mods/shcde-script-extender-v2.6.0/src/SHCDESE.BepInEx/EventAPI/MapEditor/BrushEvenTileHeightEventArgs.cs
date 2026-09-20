using System;

namespace SHCDESE.EventAPI.MapEditor;

public class BrushEvenTileHeightEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int CenterTileId { get; set; }
    public int CenterTileY { get; set; }
    public int BrushSize { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public BrushEvenTileHeightEventArgs(EventHookPhase phase, int centerTileId, int centerTileY, int brushSize)
    {
        Phase = phase;
        CenterTileId = centerTileId;
        CenterTileY = centerTileY;
        BrushSize = brushSize;
    }
}

