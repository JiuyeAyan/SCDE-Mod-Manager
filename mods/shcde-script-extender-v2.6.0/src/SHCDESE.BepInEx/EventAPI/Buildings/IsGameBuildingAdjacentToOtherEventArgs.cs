using SHCDESE.Interop;
using System;

namespace SHCDESE.EventAPI.Buildings;

public class IsGameBuildingAdjacentToOtherEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerId { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int PotentialRange { get; set; }
    public eStructs Building { get; set; }

    // --- Return Value ---
    public UInt64 ReturnValue { get; set; } = 0;

    public IsGameBuildingAdjacentToOtherEventArgs(EventHookPhase phase, int playerId, int tileX, int tileY, int potRange, eStructs building)
    {
        Phase = phase;
        PlayerId = playerId;
        TileX = tileX;
        TileY = tileY;
        PotentialRange = potRange;
        Building = building;
    }
}
