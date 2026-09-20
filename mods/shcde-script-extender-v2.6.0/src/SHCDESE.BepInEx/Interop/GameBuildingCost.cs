using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BuildingCost
{
    public int Wood;
    public int Stone;
    public int Iron;
    public int Pitch;
    public int Gold;

    public override string ToString() => $"Wood={Wood}, Stone={Stone}, Iron={Iron}, Pitch={Pitch}, Gold={Gold}";
}
