using SHCDESE.Interop;
using System;

namespace SHCDESE.Extensions;

public static class UnmanagedVector2Extensions
{
    public static UnmanagedVector2<UInt16> ToLocalPosition(this UnmanagedVector2<UInt16> vector)
    {
        return new UnmanagedVector2<UInt16>((UInt16)(vector.X / 8), (UInt16)(vector.Y / 8));
    }
    public static UnmanagedVector2<UInt16> ToWorldPosition(this UnmanagedVector2<UInt16> vector)
    {
        return new UnmanagedVector2<UInt16>((UInt16)(vector.X * 8), (UInt16)(vector.Y * 8));
    }

    public static UnmanagedVector2<Int16> ToLocalPosition(this UnmanagedVector2<Int16> vector)
    {
        return new UnmanagedVector2<Int16>((Int16)(vector.X / 8), (Int16)(vector.Y / 8));
    }
    public static UnmanagedVector2<Int16> ToWorldPosition(this UnmanagedVector2<Int16> vector)
    {
        return new UnmanagedVector2<Int16>((Int16)(vector.X * 8), (Int16)(vector.Y * 8));
    }

    public static UnmanagedVector2<UInt32> ToLocalPosition(this UnmanagedVector2<UInt32> vector)
    {
        return new UnmanagedVector2<UInt32>((UInt32)(vector.X / 8), (UInt32)(vector.Y / 8));
    }
    public static UnmanagedVector2<UInt32> ToWorldPosition(this UnmanagedVector2<UInt32> vector)
    {
        return new UnmanagedVector2<UInt32>((UInt32)(vector.X * 8), (UInt32)(vector.Y * 8));
    }

    public static UnmanagedVector2<Int32> ToLocalPosition(this UnmanagedVector2<Int32> vector)
    {
        return new UnmanagedVector2<Int32>((Int32)(vector.X / 8), (Int32)(vector.Y / 8));
    }
    public static UnmanagedVector2<Int32> ToWorldPosition(this UnmanagedVector2<Int32> vector)
    {
        return new UnmanagedVector2<Int32>((Int32)(vector.X * 8), (Int32)(vector.Y * 8));
    }
}
