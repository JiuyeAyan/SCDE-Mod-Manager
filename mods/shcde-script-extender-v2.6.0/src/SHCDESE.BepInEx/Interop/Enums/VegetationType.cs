using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// Defines the specific type of vegetation present on a map tile.
/// </summary>
public enum VegetationType : UInt16
{
    /// <summary>
    /// No vegetation is present on the tile.
    /// </summary>
    None = 0,

    /// <summary>
    /// A chestnut tree.
    /// </summary>
    ChestnutTree = 1,

    /// <summary>
    /// An oak tree.
    /// </summary>
    OakTree = 2,

    /// <summary>
    /// A pine tree.
    /// </summary>
    PineTree = 3,

    /// <summary>
    /// A birch tree.
    /// </summary>
    BirchTree = 4,

    /// <summary>
    /// A type 1 shrub, variant A.
    /// </summary>
    Shrub1A = 5,

    /// <summary>
    /// A type 1 shrub, variant B.
    /// </summary>
    Shrub1B = 6,

    /// <summary>
    /// A type 1 shrub, variant C.
    /// </summary>
    Shrub1C = 7,

    /// <summary>
    /// A type 1 shrub, variant D.
    /// </summary>
    Shrub1D = 8,

    /// <summary>
    /// A type 1 shrub, variant E.
    /// </summary>
    Shrub1E = 9,

    /// <summary>
    /// A type 2 shrub, variant A.
    /// </summary>
    Shrub2A = 0x0A,

    /// <summary>
    /// An unused shrub type.
    /// </summary>
    Shrub2B_Unused = 0x0B,

    /// <summary>
    /// An unused shrub type.
    /// </summary>
    Shrub2C_Unused = 0x0C,

    /// <summary>
    /// An unused shrub type.
    /// </summary>
    Shrub2D_Unused = 0x0D,

    /// <summary>
    /// An unused shrub type.
    /// </summary>
    Shrub2E_Unused = 0x0E,

    /// <summary>
    /// Apple Tree (exclusively used in the apple orchard)
    /// </summary>
    AppleTree = 0x0F,

    /// <summary>
    /// A type 3 shrub, variant A.
    /// </summary>
    Shrub3A = 0x10,

    /// <summary>
    /// A type 3 shrub, variant B.
    /// </summary>
    Shrub3B = 0x11,

    /// <summary>
    /// A type 3 shrub, variant C.
    /// </summary>
    Shrub3C = 0x12,

    /// <summary>
    /// A type 3 shrub, variant D.
    /// </summary>
    Shrub3D = 0x13
}