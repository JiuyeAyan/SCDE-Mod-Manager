namespace SHCDESE.Interop.Enums;

/// <summary>
/// Contains some useful TilePropertyFlag Bitmasks
/// </summary>
public static class TilePropertyMasks
{
    /// <summary>
    /// Common obstruction mask (Used by c_game_editor_place_vegetation to check for valid placement)
    /// </summary>
    public const TilePropertyFlag ObstructionMask = TilePropertyFlag.Sea | TilePropertyFlag.IsFarm | TilePropertyFlag.ImpassableEdge |
                                 TilePropertyFlag.IsWall | TilePropertyFlag.CrenelatedLow | TilePropertyFlag.IsStairs |
                                 TilePropertyFlag.IsTree | TilePropertyFlag.TreeProximity | TilePropertyFlag.PlannedMoat |
                                 TilePropertyFlag.IsLowWall | TilePropertyFlag.HasStone | TilePropertyFlag.HasIron |
                                 TilePropertyFlag.River | TilePropertyFlag.CrenelatedHigh | TilePropertyFlag.IsElevated |
                                 TilePropertyFlag.IsSwamp | TilePropertyFlag.IsMoat;

    /// <summary>
    /// A bitmask to quickly identify tiles that are fundamentally impassable for units.
    /// </summary>
    public const TilePropertyFlag ImpassableMask =
        TilePropertyFlag.Sea |
        TilePropertyFlag.ImpassableEdge |
        TilePropertyFlag.IsWall |
        TilePropertyFlag.IsBuilding |
        TilePropertyFlag.River |
        TilePropertyFlag.IsElevated;
}