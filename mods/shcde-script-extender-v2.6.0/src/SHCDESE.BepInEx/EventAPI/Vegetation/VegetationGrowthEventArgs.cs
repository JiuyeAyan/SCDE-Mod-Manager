using System;

namespace SHCDESE.EventAPI.Vegetation;

public class VegetationGrowthEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 VegetationId { get; set; }

    public VegetationGrowthEventArgs(EventHookPhase phase, Int32 vegetationId)
    {
        Phase = phase;
        VegetationId = vegetationId;
    }
}
