using MessagePack;
using System;
using System.Numerics;

namespace SHCDESE.Interop;

[MessagePackObject]
public struct UnmanagedVector2<T> where T : struct
{
    [Key(1)]
    public T X;
    [Key(2)]
    public T Y;

    public UnmanagedVector2(T x, T y)
    {
        X = x;
        Y = y;
    }

    public readonly Vector2 AsVec2f()
    {
        return new Vector2(
            (float)Convert.ChangeType(X, typeof(float)),
            (float)Convert.ChangeType(Y, typeof(float))
            );
    }

    public override string ToString()
    {
        return $"X: {X}, Y: {Y}";
    }
    public static UnmanagedVector2<T> Zero => new UnmanagedVector2<T>(default, default);
}
