using System;

namespace SHCDESE.EventAPI.Vegetation;

public class VegetationTreeDamagedEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int VegetationId { get; set; }
    public int Damage { get; set; }
    public int VegetationGlobalId { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public VegetationTreeDamagedEventArgs(EventHookPhase phase, int vegetationId, int damage, int vegetationGlobalId)
    {
        Phase = phase;
        VegetationId = vegetationId;
        Damage = damage;
        VegetationGlobalId = vegetationGlobalId;
    }
}

