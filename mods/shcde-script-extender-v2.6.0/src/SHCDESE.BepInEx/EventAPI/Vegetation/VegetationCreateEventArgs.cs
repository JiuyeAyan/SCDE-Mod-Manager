using SHCDESE.Interop.Enums;
using System;

namespace SHCDESE.EventAPI.Vegetation;

public class VegetationCreateEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public UInt16 TileX { get; set; }
    public UInt16 TileY { get; set; }
    public VegetationType VegetationType { get; set; }
    public Int16 Unknown1 { get; set; }
    public int Unknown2 { get; set; }
    public Int16 Unknown3 { get; set; }
    public int GrowthStage { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public VegetationCreateEventArgs(EventHookPhase phase, UInt16 tileX, UInt16 tileY, VegetationType vegetationType, Int16 unknown1, int unknown2, Int16 unknown3, int growthStage)
    {
        Phase = phase;
        TileX = tileX;
        TileY = tileY;
        VegetationType = vegetationType;
        Unknown1 = unknown1;
        Unknown2 = unknown2;
        Unknown3 = unknown3;
        GrowthStage = growthStage;
    }
}

