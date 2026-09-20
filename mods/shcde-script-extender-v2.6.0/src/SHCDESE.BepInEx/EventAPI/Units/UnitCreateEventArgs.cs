using SHCDESE.Interop;
using System;

namespace SHCDESE.EventAPI.Units;

public class UnitCreateEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerColorId { get; set; }
    public int PlayerOwnerId { get; set; }
    public int WorldTileX { get; set; }
    public int WorldTileY { get; set; }
    public int HeightElevation { get; set; }
    public eChimps UnitType { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public UnitCreateEventArgs(EventHookPhase phase, int playerColorId, int playerOwnerId, int worldTileX, int worldTileY, int heightElevation, eChimps unitType)
    {
        Phase = phase;
        PlayerColorId = playerColorId;
        PlayerOwnerId = playerOwnerId;
        WorldTileX = worldTileX;
        WorldTileY = worldTileY;
        HeightElevation = heightElevation;
        UnitType = unitType;
    }
}
