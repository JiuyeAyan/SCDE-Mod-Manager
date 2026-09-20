using System;

namespace SHCDESE.EventAPI.Buildings;

public class BuildingTileTakeDamageEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---

    /// <summary>
    /// The specific building tile id that has received damage
    /// </summary>
    public int TileId { get; }

    /// <summary>
    /// The specific building tile x coordinate that has received damage
    /// </summary>
    public int TileX { get; }

    /// <summary>
    /// The specific building tile y coordinate that has received damage
    /// </summary>
    public int TileY { get; }

    /// <summary>
    /// The received damage
    /// </summary>
    public int Damage { get; set; }
    public int Unknown1 { get; }

    /// <summary>
    /// The player Id responsible for the damage
    /// </summary>
    public int PlayerIdSource { get; }
    public int Unknown3 { get; }
    public int Unknown4 { get; }

    // --- Return Value ---
    public Int64 ReturnValue { get; } = 0;

    public BuildingTileTakeDamageEventArgs(EventHookPhase phase, int tileId, int tileX, int tileY, int damage, int unknown1, int playerIdSource, int unknown3, int unknown4)
    {
        Phase = phase;
        TileId = tileId;
        TileX = tileX;
        TileY = tileY;
        Damage = damage;
        Unknown1 = unknown1;
        PlayerIdSource = playerIdSource;
        Unknown3 = unknown3;
        Unknown4 = unknown4;
    }
}
