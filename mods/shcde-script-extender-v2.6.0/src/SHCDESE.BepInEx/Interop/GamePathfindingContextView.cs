using System;

namespace SHCDESE.Interop;

public unsafe sealed class GamePathfindingContextView
{
    public const int NativeComponentCapacity = 1_000;
    public const int PathConnectionRecordCapacity = 200;

    internal const int ComponentGridDirtyOffset = 0x6C;
    internal const int ComponentGenerationOffset = 0x74;
    internal const int NextComponentIdOffset = 0xCC;
    internal const int ComponentTileCountsOffset = 0xE0;
    internal const int TotalLabelledTilesOffset = 0x1080;
    internal const int ComponentVisitGenerationsOffset = 0x1084;
    internal const int PathConnectionRecordsOffset = 0x2024;

    private readonly Byte* _ptr;

    public GamePathfindingContextView(IntPtr address)
    {
        _ptr = (Byte*)address;
    }

    public IntPtr Address => (IntPtr)_ptr;

    public ref Int32 ComponentGridDirty => ref *(Int32*)(_ptr + ComponentGridDirtyOffset);

    public ref UInt32 ComponentGeneration => ref *(UInt32*)(_ptr + ComponentGenerationOffset);

    public ref Int32 NextComponentId => ref *(Int32*)(_ptr + NextComponentIdOffset);

    /// <summary>
    /// Live stock component-count table. It has exactly 1,000 entries; indexing it with a PCL above 999 is invalid.
    /// </summary>
    public Span<Int32> ComponentTileCounts => new Span<Int32>(_ptr + ComponentTileCountsOffset, NativeComponentCapacity);

    public ref Int32 TotalLabelledTiles => ref *(Int32*)(_ptr + TotalLabelledTilesOffset);

    /// <summary>
    /// Live stock component visit-generation table. It has exactly 1,000 entries.
    /// </summary>
    public Span<Int32> ComponentVisitGenerations => new Span<Int32>(_ptr + ComponentVisitGenerationsOffset, NativeComponentCapacity);

    /// <summary>
    /// Live macro-connection records, including reserved record zero.
    /// </summary>
    public Span<PathConnectionRecord> PathConnectionRecords => new Span<PathConnectionRecord>(_ptr + PathConnectionRecordsOffset, PathConnectionRecordCapacity);
}
