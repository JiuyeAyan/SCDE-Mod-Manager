using SHCDESE.Interop.Enums;
using System;

namespace SHCDESE.EventAPI.Tribes;

public class TribeIssueOrderMoveHereEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int TribeId { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public Int16 IsPatrolPath { get; set; }
    public bool IsNewOrder { get; set; }
    public TribeMoveType MoveType { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public TribeIssueOrderMoveHereEventArgs(EventHookPhase phase, int tribeId, int tileX, int tileY, Int16 bIsPatrolPath, bool bIsNewOrder, TribeMoveType tribeMoveType)
    {
        Phase = phase;
        TribeId = tribeId;
        TileX = tileX;
        TileY = tileY;
        IsPatrolPath = bIsPatrolPath;
        IsNewOrder = bIsNewOrder;
        this.MoveType = tribeMoveType;
    }
}
