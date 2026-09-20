using SHCDESE.Interop;
using System;

namespace SHCDESE.API.Components.Trigger;
public class CircleTriggerZone(int handle, int centerX, int centerY, int radius) : TriggerZone(handle)
{
    public int CenterX = centerX, CenterY = centerY, Radius = radius;
    private readonly long _radiusSquared = (long)radius * radius; // Pre-calculated

    public override bool Contains(UnmanagedVector2<UInt16> pos)
    {
        long dX = pos.X - CenterX;
        long dY = pos.Y - CenterY;
        return (dX * dX + dY * dY) <= _radiusSquared;
    }
}