using System.Numerics;
using System.Runtime.CompilerServices;

namespace SHCDESE.Extensions;

/// <summary>
/// A collection of conversion utilities for UnityEngine.Vector3
/// </summary>
public static class UnityEngineVector3Extensions
{
    /// <summary>
    /// Convert a given Vector3 to a UnityEngine.Vector2
    /// (safe-variant, alloc + copy-based)
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnityEngine.Vector3 ToUnityVector3Safe(this Vector3 v)
    {
        return new UnityEngine.Vector3(v.X, v.Y, v.Z);
    }

    /// <summary>
    /// Convert a given Vector3 to a System.Numerics.Vector3
    /// (nearly zero-overhead, by-value argument)
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe static UnityEngine.Vector3* ToUnityVector3PtrByValue(this Vector3 v)
    {
        return (UnityEngine.Vector3*)&v;
    }

    /// <summary>
    /// Convert a given Vector3 to a UnityEngine.Vector3
    /// (zero-overhead, by-ref argument; unless JIT ignores inlining)
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe static UnityEngine.Vector3* ToUnityVector3PtrByRef(this ref Vector3 v)
    {
        return (UnityEngine.Vector3*)Unsafe.AsPointer(ref v);
    }

    /// <summary>
    /// Convert a given Vector3 to a UnityEngine..Vector3
    /// (nearly zero-overhead, by-value argument)
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe static ref UnityEngine.Vector3 ToUnityVector3RefByValue(this Vector3 v)
    {
        return ref Unsafe.AsRef<UnityEngine.Vector3>(&v);
    }

    /// <summary>
    /// Convert a given Vector3 to a UnityEngine.Vector3
    /// (zero-overhead, by-ref argument; unless JIT ignores inlining)
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe static ref UnityEngine.Vector3 ToUnityVector2RefByRef(this ref Vector3 v)
    {
        return ref Unsafe.AsRef<UnityEngine.Vector3>(Unsafe.AsPointer(ref v));
    }

}
