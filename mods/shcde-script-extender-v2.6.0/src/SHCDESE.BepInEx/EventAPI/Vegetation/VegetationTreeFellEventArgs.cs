using System;

namespace SHCDESE.EventAPI.Vegetation;

public class VegetationTreeFellEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int VegetationId { get; set; }
    public int VegetationGlobalId { get; set; }

    public VegetationTreeFellEventArgs(EventHookPhase phase, int vegetationId, int vegetationGlobalId)
    {
        Phase = phase;
        VegetationId = vegetationId;
        VegetationGlobalId = vegetationGlobalId;
    }
}

