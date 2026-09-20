using System;

namespace SHCDESE.EventAPI.Vegetation;

public class VegetationSubtractResourceEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int VegetationId { get; set; }
    public int VegetationGlobalId { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public VegetationSubtractResourceEventArgs(EventHookPhase phase, int vegetationId, int vegetationGlobalId)
    {
        Phase = phase;
        VegetationId = vegetationId;
        VegetationGlobalId = vegetationGlobalId;
    }
}

