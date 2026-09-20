using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop.VTables;

/// <summary>
/// I know its technically not a VTable but it surely behaves like one. 
/// Function Table, VTable, who cares lmao.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public unsafe struct GatehouseFunctionsVTable
{
    public delegate* unmanaged[Cdecl]<void> Nullsub0;
    public delegate* unmanaged[Cdecl]<Int64> Unknown1;
    public delegate* unmanaged[Cdecl]<void> Nullsub2;
    public delegate* unmanaged[Cdecl]<Int64> GatehouseBigUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GatehouseSmallUpdate;
    public delegate* unmanaged[Cdecl]<Int64> Nullsub5;
    public delegate* unmanaged[Cdecl]<Int64> Unknown6;
    public delegate* unmanaged[Cdecl]<Int64> Nullsub7;
    public delegate* unmanaged[Cdecl]<Int64> Nullsub8;
}
