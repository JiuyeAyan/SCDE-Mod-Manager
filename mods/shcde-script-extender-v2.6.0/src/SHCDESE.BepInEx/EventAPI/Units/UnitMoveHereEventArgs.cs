using System;

namespace SHCDESE.EventAPI.Units;

public class UnitMoveHereEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int UnitId { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int Unknown { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public UnitMoveHereEventArgs(EventHookPhase phase, int unitId, int tileX, int tileY, int unknown)
    {
        Phase = phase;
        UnitId = unitId;
        TileX = tileX;
        TileY = tileY;
        Unknown = unknown;
    }
}
