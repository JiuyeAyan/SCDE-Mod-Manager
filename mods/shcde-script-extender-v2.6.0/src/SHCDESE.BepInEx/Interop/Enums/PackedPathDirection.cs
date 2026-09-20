namespace SHCDESE.Interop.Enums;

/// <summary>
/// Direction values stored two per byte in a native unit path plan.
/// These values are zero-based and must not be cast directly to <see cref="Dircs"/>.
/// </summary>
public enum PackedPathDirection : byte
{
    North = 0,
    NorthEast = 1,
    East = 2,
    SouthEast = 3,
    South = 4,
    SouthWest = 5,
    West = 6,
    NorthWest = 7
}