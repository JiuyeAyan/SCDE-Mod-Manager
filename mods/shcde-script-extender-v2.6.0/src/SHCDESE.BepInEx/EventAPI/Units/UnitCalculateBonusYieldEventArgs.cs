using System;

namespace SHCDESE.EventAPI.Units;

public class UnitCalculateBonusYieldEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int UnitId { get; set; }
    public int GoodAmount { get; set; }
    public int b50PercentBonus { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public UnitCalculateBonusYieldEventArgs(EventHookPhase phase, int unitId, int goodAmount, int b50PercentBonus)
    {
        Phase = phase;
        UnitId = unitId;
        GoodAmount = goodAmount;
        this.b50PercentBonus = b50PercentBonus;
    }
}
