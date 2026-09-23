using SHCDESE.Interop.Enums;
using System;

namespace SHCDESE.Interop;

/// <summary>
/// A live pointer-backed view of one unit's nibble-packed path plan.
/// This view does not copy native memory.
/// </summary>
public unsafe sealed class GameUnitPathPlanView
{
    private readonly GameUnit* _unit;
    private readonly Byte* _packedPlan;

    internal GameUnitPathPlanView(GameUnit* unit, Byte* packedPlan)
    {
        _unit = unit;
        _packedPlan = packedPlan;
    }

    public IntPtr Address => (IntPtr)_packedPlan;

    /// <summary>The live active transition count, clamped to physical plan capacity.</summary>
    public Int32 Count => Math.Min((Int32)Length, GameUnitManager.PackedPathPlanTransitionsPerUnit);

    /// <summary>
    /// Reads or writes one decoded direction directly in native memory.
    /// This is a live view; it is not a copied direction array.
    /// </summary>
    public PackedPathDirection this[Int32 transitionIndex]
    {
        get
        {
            if (!TryGetDirection(transitionIndex, out PackedPathDirection direction))
                throw new ArgumentOutOfRangeException(nameof(transitionIndex));

            return direction;
        }
        set
        {
            if (!TrySetDirection(transitionIndex, value))
                throw new ArgumentOutOfRangeException(nameof(transitionIndex));
        }
    }

    /// <summary>The live native transition count stored in GameUnit.r_PathPlanLength.</summary>
    public ref UInt16 Length => ref _unit->r_PathPlanLength;

    /// <summary>The live native transition cursor stored in GameUnit.r_CurrentPathPlanIndex.</summary>
    public ref UInt16 CurrentIndex => ref _unit->r_CurrentPathPlanIndex;

    /// <summary>Returns the complete live writable 1,000-byte packed buffer.</summary>
    public Span<Byte> PackedBytes => new Span<Byte>(_packedPlan, GameUnitManager.PackedPathPlanBytesPerUnit);

    /// <summary>
    /// Returns the part of the live packed buffer occupied by the current plan.
    /// A corrupt native length is clamped to the physical buffer capacity.
    /// </summary>
    public Span<Byte> ActivePackedBytes
    {
        get
        {
            Int32 transitionCount = Count;
            return new Span<Byte>(_packedPlan, (transitionCount + 1) >> 1);
        }
    }

    public bool IsLengthValid => Length <= GameUnitManager.PackedPathPlanTransitionsPerUnit;

    /// <summary>Reads an active direction nibble.</summary>
    public bool TryGetDirection(Int32 transitionIndex, out PackedPathDirection direction)
    {
        direction = default;
        if ((UInt32)transitionIndex >= Length)
            return false;

        return TryGetRawDirection(transitionIndex, out direction);
    }

    /// <summary>Writes an active direction nibble and preserves the adjacent nibble.</summary>
    public bool TrySetDirection(Int32 transitionIndex, PackedPathDirection direction)
    {
        return (UInt32)transitionIndex < Length && TrySetRawDirection(transitionIndex, direction);
    }

    /// <summary>Reads any nibble inside the physical 2,000-transition capacity, including inactive storage.</summary>
    public bool TryGetRawDirection(Int32 transitionIndex, out PackedPathDirection direction)
    {
        direction = default;
        if ((UInt32)transitionIndex >= GameUnitManager.PackedPathPlanTransitionsPerUnit)
            return false;

        Byte packed = _packedPlan[transitionIndex >> 1];
        Byte rawDirection = (Byte)((packed >> ((transitionIndex & 1) * 4)) & 0x0F);
        if (rawDirection > (Byte)PackedPathDirection.NorthWest)
            return false;

        direction = (PackedPathDirection)rawDirection;
        return true;
    }

    /// <summary>Writes any nibble inside the physical capacity and preserves the adjacent nibble.</summary>
    public bool TrySetRawDirection(Int32 transitionIndex, PackedPathDirection direction)
    {
        if ((UInt32)transitionIndex >= GameUnitManager.PackedPathPlanTransitionsPerUnit || (UInt32)direction > (UInt32)PackedPathDirection.NorthWest)
        {
            return false;
        }

        Int32 byteIndex = transitionIndex >> 1;
        Int32 shift = (transitionIndex & 1) * 4;
        Byte nibbleMask = (Byte)(0x0F << shift);
        _packedPlan[byteIndex] = (Byte)((_packedPlan[byteIndex] & ~nibbleMask) | ((Byte)direction << shift));
        return true;
    }

    /// <summary>Changes the active plan length without constructing a new path.</summary>
    public bool TrySetLength(Int32 transitionCount, bool clampCurrentIndex = true)
    {
        if ((UInt32)transitionCount > GameUnitManager.PackedPathPlanTransitionsPerUnit)
            return false;

        Length = (UInt16)transitionCount;
        if (clampCurrentIndex && CurrentIndex > transitionCount)
            CurrentIndex = (UInt16)transitionCount;

        return true;
    }

    /// <summary>Changes the current transition cursor without changing the packed directions.</summary>
    public bool TrySetCurrentIndex(Int32 transitionIndex)
    {
        if ((UInt32)transitionIndex > Length || transitionIndex > GameUnitManager.PackedPathPlanTransitionsPerUnit)
        {
            return false;
        }

        CurrentIndex = (UInt16)transitionIndex;
        return true;
    }

    /// <summary>Replaces the complete active plan and resets the current transition cursor.</summary>
    public bool TryReplace(ReadOnlySpan<PackedPathDirection> directions, Int32 currentIndex = 0)
    {
        if (directions.Length > GameUnitManager.PackedPathPlanTransitionsPerUnit || (UInt32)currentIndex > (UInt32)directions.Length)
        {
            return false;
        }

        for (Int32 i = 0; i < directions.Length; i++)
        {
            if ((UInt32)directions[i] > (UInt32)PackedPathDirection.NorthWest)
                return false;
        }

        PackedBytes.Clear();
        for (Int32 i = 0; i < directions.Length; i++)
            TrySetRawDirection(i, directions[i]);

        Length = (UInt16)directions.Length;
        CurrentIndex = (UInt16)currentIndex;
        return true;
    }

    /// <summary>Clears the full packed buffer and resets both path-plan counters.</summary>
    public void Clear()
    {
        PackedBytes.Clear();
        Length = 0;
        CurrentIndex = 0;
    }
}
