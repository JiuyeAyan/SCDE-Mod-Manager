using System;

namespace SHCDESE.EventAPI.MapEditor;

public class BrushDeleteEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int CenterTileX { get; set; }
    public int CenterTileY { get; set; }
    public int BrushSize { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public BrushDeleteEventArgs(EventHookPhase phase, int centerTileX, int centerTileY, int brushSize)
    {
        Phase = phase;
        CenterTileX = centerTileX;
        CenterTileY = centerTileY;
        BrushSize = brushSize;
    }
}

