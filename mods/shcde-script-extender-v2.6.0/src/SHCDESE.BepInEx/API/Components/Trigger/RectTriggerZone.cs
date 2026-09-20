using SHCDESE.Interop;
using System;

namespace SHCDESE.API.Components.Trigger;

public class RectTriggerZone(int handle, int x, int y, int width, int height) : TriggerZone(handle)
{
    public int X = x, Y = y, Width = width, Height = height;

    public override bool Contains(UnmanagedVector2<UInt16> pos)
    {
        return pos.X >= X && pos.X < X + Width && pos.Y >= Y && pos.Y < Y + Height;
    }
}