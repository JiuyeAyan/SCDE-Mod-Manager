using System;

namespace SHCDESE.EventAPI.Vegetation;

public class VegetationDeleteEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int VegetationId { get; set; }

    public VegetationDeleteEventArgs(EventHookPhase phase, int vegetationId)
    {
        Phase = phase;
        VegetationId = vegetationId;
    }
}

