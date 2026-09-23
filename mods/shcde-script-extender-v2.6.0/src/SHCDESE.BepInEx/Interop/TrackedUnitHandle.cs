using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TrackedUnitHandle
{
    public Int32 unitId;
    public Int32 unitGlobalId;
}