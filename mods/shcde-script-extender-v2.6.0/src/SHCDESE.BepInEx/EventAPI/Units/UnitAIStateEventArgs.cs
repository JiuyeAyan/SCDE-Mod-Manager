using SHCDESE.Interop;
using System;

namespace SHCDESE.EventAPI.Units;

public class UnitAIStateEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; }
    public eChimps UnitType { get; }
    public int State { get; set; }

    public UnitAIStateEventArgs(EventHookPhase phase, Int32 unitId, eChimps unitType, int state)
    {
        Phase = phase;
        UnitId = unitId;
        UnitType = unitType;
        State = state;
    }
}
