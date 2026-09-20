using System;

namespace SHCDESE.EventAPI.Units;

public class UnitEnterKillingPitEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; }
    public Int32 BuildingId { get;}
    public Int32 CurrentHealth { get; }
    public Int32 Damage { get; set; }

    public UnitEnterKillingPitEventArgs(EventHookPhase phase, Int32 unitId, int buildingId, int currentHealth, int damage)
    {
        Phase = phase;
        UnitId = unitId;
        BuildingId = buildingId;
        CurrentHealth = currentHealth;
        Damage = damage;
    }
}
