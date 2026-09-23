using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

/// <summary>
/// Per-player defensive-position tile IDs, laid out as 30 consecutive classes with 10 slots per class.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct GameAIDefensivePositionTileIdTable
{
    public const int ClassCount = 30;
    public const int SlotsPerClass = 10;
    public const int Capacity = ClassCount * SlotsPerClass;

    public fixed UInt32 r_TileIds[Capacity];
}

/// <summary>Per-player active defensive-position count for each of the 30 defensive classes.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct GameAIDefensivePositionCountTable
{
    public const int Capacity = GameAIDefensivePositionTileIdTable.ClassCount;

    public fixed Int32 r_Counts[Capacity];
}
