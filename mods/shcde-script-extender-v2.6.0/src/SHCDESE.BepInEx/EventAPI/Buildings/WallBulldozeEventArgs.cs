using System;

namespace SHCDESE.EventAPI.Buildings;

public class WallBulldozeEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerId { get; set; }
    public int TileId { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; } = 0;

    public WallBulldozeEventArgs(EventHookPhase phase, int playerId, int tileId)
    {
        Phase = phase;
        PlayerId = playerId;
        TileId = tileId;
    }
}
