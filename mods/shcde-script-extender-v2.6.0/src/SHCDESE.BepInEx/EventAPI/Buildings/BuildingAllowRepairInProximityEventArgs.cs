using System;

namespace SHCDESE.EventAPI.Buildings;

public class BuildingAllowRepairInProximityEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerId { get; }
    public int TileX { get; }
    public int TileY { get; set; }
    public int Proximity { get; set; }
    public byte Unknown { get; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public BuildingAllowRepairInProximityEventArgs(EventHookPhase phase, int playerId, int tileX, int tileY, int proximity, byte unknown)
    {
        Phase = phase;
        PlayerId = playerId; 
        TileX = tileX;
        TileY = tileY;
        Proximity = proximity;
        Unknown = unknown;
    }
}
