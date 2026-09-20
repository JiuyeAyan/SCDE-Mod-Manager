using SHCDESE.Interop;
using System;

namespace SHCDESE.EventAPI.Buildings;

public class KeepSpawnPeasantEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerColorId { get; set; }
    public int PlayerOwnerId { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int HeightElevation { get; set; }
    public eChimps Chimp { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public KeepSpawnPeasantEventArgs(EventHookPhase phase, int playerColorId, int playerOwnerId, int tileX, int tileY, int heightElevation, eChimps chimp)
    {
        Phase = phase;
        PlayerColorId = playerColorId;
        PlayerOwnerId = playerOwnerId;
        TileX = tileX;
        TileY = tileY;
        HeightElevation = heightElevation;
        Chimp = chimp;
    }
}
