using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

[StructLayout(LayoutKind.Sequential)]
public struct AxisDistanceMetrics
{
    public Int32 dx;
    public Int32 dy;
    public Int32 minDelta;
    public Int32 maxDelta;
}
