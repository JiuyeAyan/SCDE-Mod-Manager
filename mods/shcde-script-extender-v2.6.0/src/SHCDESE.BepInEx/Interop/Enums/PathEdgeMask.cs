using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// Bits in the per-tile native path-edge mask.
/// </summary>
[Flags]
public enum PathEdgeMask : byte
{
    None = 0,
    North = 1 << 0,
    NorthEast = 1 << 1,
    East = 1 << 2,
    SouthEast = 1 << 3,
    South = 1 << 4,
    SouthWest = 1 << 5,
    West = 1 << 6,
    NorthWest = 1 << 7
}
