using System;

namespace SHCDESE.EventAPI.Units;

public class UnitHealByBedouinHealerEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 HealedUnitId { get; }
    public Int32 HealerUnitId { get; }
    public Int32 Heal { get; set; }

    public UnitHealByBedouinHealerEventArgs(EventHookPhase phase, Int32 healedUnitId, Int32 healerUnitId, Int32 heal)
    {
        Phase = phase;
        HealedUnitId = healedUnitId;
        HealerUnitId = HealerUnitId;
        Heal = heal;
    }
}
