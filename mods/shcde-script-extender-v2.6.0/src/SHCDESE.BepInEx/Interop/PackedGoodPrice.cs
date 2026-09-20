using SHCDESE.Lua.CodeGen;
using System;
using System.Runtime.InteropServices;
namespace SHCDESE.Interop;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PackedGoodPrice
{
    [LuaExposed] public Int32 BuyPrice;
    [LuaExposed] public Int32 SellPrice;

    public PackedGoodPrice(int buyPrice, int sellPrice)
    {
        BuyPrice = buyPrice;
        SellPrice = sellPrice;
    }
}