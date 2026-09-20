using SHCDESE.Interop.Enums;
using System;

namespace SHCDESE.EventAPI.Tribes;

public class TribeIssueOrderWithTargetEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int TribeId { get; set; }
    public TribeAICommand AICommand { get; set; }
    public Int32 TargetValue1 { get; set; }
    public Int32 TargetValue2 { get; set; }
    public int a6 { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public TribeIssueOrderWithTargetEventArgs(EventHookPhase phase, int tribeId, TribeAICommand aiCommand, Int32 targetValue1, Int32 targetValue2, int a6)
    {
        Phase = phase;
        TribeId = tribeId;
        AICommand = aiCommand;
        TargetValue1 = targetValue1;
        TargetValue2 = targetValue2;
        this.a6 = a6;
    }
}
