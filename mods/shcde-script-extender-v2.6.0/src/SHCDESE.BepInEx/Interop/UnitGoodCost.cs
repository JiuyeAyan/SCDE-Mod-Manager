using SHCDESE.Lua.CodeGen;
using System.Runtime.InteropServices;
using SHCDESE.Extensions;

namespace SHCDESE.Interop;

[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
public struct UnitGoodCosts
{
    [LuaExposed] public eGoods32 cost1;
    [LuaExposed] public eGoods32 cost2;
    [LuaExposed] public eGoods32 cost3;
    [LuaExposed] public eGoods32 cost4;

    public UnitGoodCosts(eGoods32 cost1, eGoods32 cost2, eGoods32 cost3, eGoods32 cost4)
    {
        this.cost1 = cost1;
        this.cost2 = cost2;
        this.cost3 = cost3;
        this.cost4 = cost4;
    }

    public bool HasCosts(eGoods good)
    {
        //if (cost1 == eGoods32._SE_REQUIRE_HORSE && good == eGoods._SE_REQUIRE_HORSE) return true;
        //if (cost1 == good) return true;
        //if (cost2 == good) return true;
        //if (cost3 == good) return true;
        //if (cost4 == good) return true;

        if (cost1.IsSameGood(good)) return true;
        if (cost2.IsSameGood(good)) return true;
        if (cost3.IsSameGood(good)) return true;
        if (cost4.IsSameGood(good)) return true;

        return false;
    }

    public override string ToString()
    {
        return $"cost1: {cost1}, cost2: {cost2}, cost3: {cost3}, cost4: {cost4}";
    }
}
