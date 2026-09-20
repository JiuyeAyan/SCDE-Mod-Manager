using CrusaderDE;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Logging;
using System;
using System.Reflection;

namespace SHCDESE.ManagedHooks.ILHooks;

public partial class ILHooksManager
{
    //
    // MainViewModel: Hook all eu unit on-hover logic.
    //
    internal ILHook? mainViewModel_buttonEnterCreateTroop;

    /// <summary>
    /// This ILHook attempts to find and NOP-out all the onhover logic for european units
    /// that shows what good a unit costs. Useful for changing unit good costs visually.
    /// 
    /// Logic:
    /// - Finds 7 matches of "this.lastTroopBuildChimp = " (ignores first occurence)
    /// - Finds every start of "this.setCreateTroopText();"
    /// - NOPS out everything in between and inserts static lambda callback.
    /// </summary>
    /// <param name="ctx">ILContext for this method</param>
    internal void MainViewModel_ButtonEnterCreateTroop_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        int startIndex = 0;
        int endIndex = 0;
        int count = 0;
        int it = 0;
        //LogHelper.Debug($"IL ={ctx.ToString()}");

        while (c.TryGotoNext(MoveType.Before, x => x.MatchStfld(typeof(MainViewModel), nameof(MainViewModel.lastTroopBuildChimp))) && count < 7)
        {
            it++;
            // skip first found element
            if (it == 1)
                continue;

            // 42  0065    ldarg.0
            // 43  0066    ldc.i4.s    0x16 [c.Instrs[c.Index--].Operand;]
            // 44  0068    stfld valuetype Enums / eChimps CrusaderDE.MainViewModel::lastTroopBuildChimp
            startIndex = c.Index + 1;
            LogHelper.Debug($"BLOCK 1: operand={c.Instrs[c.Index--].Operand}, instr={c.Index}");
            if (c.TryGotoNext(MoveType.Before, y => y.MatchCall(typeof(MainViewModel), nameof(MainViewModel.setCreateTroopText))))
            {
                LogHelper.Debug($"BLOCK 2: Found endIndex: {c.Index}");
                endIndex = c.Index - 1;

                LogHelper.Debug($"BLOCK 2: Going to start index... {startIndex} from current: {c.Index}");
                c.Goto(startIndex);
                for (int i = startIndex; i < endIndex; i++)
                {
                    LogHelper.Debug($"BLOCK 3: EMITTING NOPS: {c.Index} < {endIndex}");
                    ctx.Body.Instructions[i].OpCode = OpCodes.Nop;
                    ctx.Body.Instructions[i].Operand = null;
                }
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldfld, typeof(MainViewModel).GetField(nameof(MainViewModel.lastTroopBuildChimp), BindingFlags.Instance | BindingFlags.Public));
                c.EmitDelegate(MainViewModel_ButtonEnterCreateTroop_Callback);
            }
            c.Index = endIndex + 1;
            count++;
            LogHelper.Debug($"BLOCK 1: Set index={c.Index}, count: {count}");
        }
        LogHelper.Debug($"Processed {count} found matches");

        LogHelper.Debug($"Processed {count} blocks");
        LogHelper.Verbose($"IL={ctx.ToString()}");
    }

    /// <summary>
    /// Higher level C# callback companion to MainViewModel_ButtonEnterCreateTroop_ILHook
    /// Emulates the hardcoded of showing the unit good cost requirements
    /// behaviour by the game in a more modular way.
    /// </summary>
    /// <param name="chimp">The chimp in question</param>
    internal static void MainViewModel_ButtonEnterCreateTroop_Callback(eChimps chimp)
    {
        try
        {
            UnitGoodCosts goodCost = GameUnitManagerAPI.Instance.GetUnitGoodCosts(chimp);
            //Log.Debug($"MainViewModel_ButtonEnterCreateTroop_ILHook: {chimp}, costs={goodCost.ToString()}");

            if (goodCost.HasCosts(eGoods.STORED_BOWS))
                MainViewModel.Instance.Show_BarracksBows2 = true;

            if (goodCost.HasCosts(eGoods.STORED_SPEARS))
                MainViewModel.Instance.Show_BarracksSpears2 = true;

            if (goodCost.HasCosts(eGoods.STORED_MACES))
                MainViewModel.Instance.Show_BarracksMaces2 = true;

            if (goodCost.HasCosts(eGoods.STORED_CROSSBOWS))
                MainViewModel.Instance.Show_BarracksXBows2 = true;

            if (goodCost.HasCosts(eGoods.STORED_LEATHER_ARMOUR))
                MainViewModel.Instance.Show_BarracksLeatherArmour2 = true;

            if (goodCost.HasCosts(eGoods.STORED_PIKES))
                MainViewModel.Instance.Show_BarracksPikes2 = true;

            if (goodCost.HasCosts(eGoods.STORED_SWORDS))
                MainViewModel.Instance.Show_BarracksSwords2 = true;

            if (goodCost.HasCosts(eGoods.STORED_METAL_ARMOUR))
                MainViewModel.Instance.Show_BarracksArmour2 = true;

            // default swordsman handling (horse requirement)
            if (goodCost.HasCosts(eGoods._SE_REQUIRE_HORSE))
                //if (chimp == eChimps.CHIMP_TYPE_KNIGHT)
            {
                MainViewModel.instance.Show_BarracksHorses2 = true;
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error");
        }
    }
}
