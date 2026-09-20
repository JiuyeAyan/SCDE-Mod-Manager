using System;

namespace SHCDESE.EventAPI.Projectiles;

public class SpawnFireEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int SourcePlayerId { get; set; }
    public int WorldTileX { get; set; }
    public int WorldTileY { get; set; }
    public int HeightElevation { get; set; }
    public int SpreadRadius { get; set; }
    public int A6 { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public SpawnFireEventArgs(EventHookPhase phase, int sourcePlayerId, int worldTileX, int worldTileY, int heightElevation, int spreadRadius, int a6)
    {
        Phase = phase;
        SourcePlayerId = sourcePlayerId;
        WorldTileX = worldTileX;
        WorldTileY = worldTileY;
        HeightElevation = heightElevation;
        SpreadRadius = spreadRadius;
        A6 = a6;
    }
}
