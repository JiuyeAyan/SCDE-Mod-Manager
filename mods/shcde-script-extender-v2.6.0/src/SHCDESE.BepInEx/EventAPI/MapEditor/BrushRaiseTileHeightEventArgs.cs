using System;

namespace SHCDESE.EventAPI.MapEditor;

public class BrushRaiseTileHeightEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int CenterTileId { get; set; }
    public int CenterTileY { get; set; }
    public int BrushSize { get; set; }
    public int RaiseModifier { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public BrushRaiseTileHeightEventArgs(EventHookPhase phase, int centerTileId, int centerTileY, int brushSize, int raiseModifier)
    {
        Phase = phase;
        CenterTileId = centerTileId;
        CenterTileY = centerTileY;
        BrushSize = brushSize;
        RaiseModifier = raiseModifier;
    }
}

