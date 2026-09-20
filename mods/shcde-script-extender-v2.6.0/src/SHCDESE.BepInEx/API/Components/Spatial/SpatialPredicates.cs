using SHCDESE.Interop;
using SHCDESE.Interop.Query;
using System;

namespace SHCDESE.API.Components.Spatial;

public static class SpatialPredicates
{
    /// <summary>
    /// Creates a predicate that checks if an item is within a rectangular area.
    /// </summary>
    public unsafe static RefPredicate<T> IsWithinRect<T>(int x, int y, int width, int height) where T : unmanaged, IPositionable
    {
        return (in T item) =>
        {
            UnmanagedVector2<ushort>* pos = item.CurrentTilePosition();
            return pos->X >= x &&
                   pos->X < x + width &&
                   pos->Y >= y &&
                   pos->Y < y + height;
        };
    }

    /// <summary>
    /// Creates a predicate that checks if an item is within a spherical/circular area.
    /// </summary>
    public unsafe static RefPredicate<T> IsWithinSphere<T>(int centerX, int centerY, int radius) where T : unmanaged, IPositionable
    {
        long rangeSquared = (long)radius * radius;
        return (in T item) =>
        {
            UnmanagedVector2<ushort>* pos = item.CurrentTilePosition();
            long dX = pos->X - centerX;
            long dY = pos->Y - centerY;
            return (dX * dX + dY * dY) <= rangeSquared;
        };
    }
}
