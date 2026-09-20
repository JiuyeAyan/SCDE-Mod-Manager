using SHCDESE.Interop;
using System;

namespace SHCDESE.EventAPI.MapEditor;

public class UnitPlaceEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int16 Unknown1 { get; set; }
    public Int16 Unknown2 { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int PlayerId { get; set; }
    public eChimps UnitType { get; set; }
    public UInt32 Unknown3 { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public UnitPlaceEventArgs(EventHookPhase phase, Int16 unknown1, Int16 unknown2, int tileX, int tileY, int playerId, eChimps unitType, UInt32 unknown3)
    {
        Phase = phase;
        Unknown1 = unknown1;
        Unknown2 = unknown2;
        TileX = tileX;
        TileY = tileY;
        PlayerId = playerId;
        UnitType = unitType;
        Unknown3 = unknown3;
    }
}

