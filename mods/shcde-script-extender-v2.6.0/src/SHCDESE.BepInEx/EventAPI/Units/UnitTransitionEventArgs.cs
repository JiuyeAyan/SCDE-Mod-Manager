using SHCDESE.Interop;
using System;

namespace SHCDESE.EventAPI.Units;

[Flags]
public enum UnitTransitionSource
{
    MercenaryOutpost = 0,
    EuropeanBarracks = 1,
    Worker = 2,
    Disband = 4
}

public class UnitTransitionEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int UnitId { get; }
    public int PlayerOwnerId { get; }
    public eChimps NextUnitType { get; set; }
    public UnitTransitionSource Source { get; }

    public UnitTransitionEventArgs(EventHookPhase phase, int unitId, int playerOwnerId, eChimps nextUnitType, UnitTransitionSource source)
    {
        Phase = phase;

        UnitId = unitId;
        PlayerOwnerId = playerOwnerId;
        NextUnitType = nextUnitType;
        Source = source;
    }
}
