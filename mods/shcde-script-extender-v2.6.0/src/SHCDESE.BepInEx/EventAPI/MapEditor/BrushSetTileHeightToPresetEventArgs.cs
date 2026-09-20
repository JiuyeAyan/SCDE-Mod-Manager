using System;

namespace SHCDESE.EventAPI.MapEditor;

public class BrushSetTileHeightToPresetEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int CenterTileId { get; set; }
    public int CenterTileY { get; set; }
    public int BrushSize { get; set; }
    public int HeightPreset { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public BrushSetTileHeightToPresetEventArgs(EventHookPhase phase, int centerTileId, int centerTileY, int brushSize, int heightPreset)
    {
        Phase = phase;
        CenterTileId = centerTileId;
        CenterTileY = centerTileY;
        BrushSize = brushSize;
        HeightPreset = heightPreset;
    }
}

