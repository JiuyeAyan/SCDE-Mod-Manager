using System;

namespace SHCDESE.Interop.Enums;


/// <summary>
/// Represents the visual texture and features of a map tile as a set of bit flags.
/// </summary>
[Flags]
public enum TileType : byte
{
    /// <summary>
    /// The default state, likely standard dirt.
    /// </summary>
    NoneOrDirt = 0,

    /// <summary>
    /// The tile has some light foliage or grass cover. (Bit 0)
    /// </summary>
    Foliage = 1 << 0, // 0x01

    /// <summary>
    /// The tile has small stones mixed with dirt. (Bit 1)
    /// </summary>
    DirtAndStones = 1 << 1, // 0x02

    /// <summary>
    /// The tile has a specific type for being on the first level of elevation. (Bit 2)
    /// Only appears when we elevate something to the first level?
    /// </summary>
    Elevation1 = 1 << 2, // 0x04

    /// <summary>
    /// The tile has a specific type for being on the second level of elevation. (Bit 3)
    /// Only appears when we elevate something to the second level?
    /// </summary>
    Elevation2 = 1 << 3, // 0x08

    /// <summary>
    /// The tile has lush oasis grass. (Bit 4)
    /// </summary>
    OasisGrass = 1 << 4, // 0x10

    /// <summary>
    /// The tile is beach sand or has small sea waves. The game uses the same bit for both. (Bit 5)
    /// </summary>
    BeachOrWaves = 1 << 5, // 0x20

    /// <summary>
    /// The tile is coarse desert sand. (Bit 6)
    /// </summary>
    CoarseSand = 1 << 6, // 0x40

    /// <summary>
    /// The tile has thick, dense foliage. (Bit 7)
    /// </summary>
    ThickFoliage = 1 << 7, // 0x80
}