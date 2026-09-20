using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.Logging;

namespace SHCDESE.ManagedHooks.ILHooks;

public partial class ILHooksManager
{
    //
    // Director:
    //
    internal ILHook? director_IncreaseFrameRate;
    internal void Director_IncreaseFrameRate_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldc_I4_S),
            x => x.Match(OpCodes.Stloc_0),
            x => x.Match(OpCodes.Call),
            x => x.Match(OpCodes.Callvirt)
            ))
        {
            LogHelper.Error($"Target 1 not found!");
            return;
        }
        c.RemoveRange(1);
        c.Emit(OpCodes.Ldc_I4, (int)Plugin.Instance.MaxGameSpeed.Value);

        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldc_I4),
            x => x.Match(OpCodes.Stloc_0),
            x => x.Match(OpCodes.Ldc_R8),
            x => x.Match(OpCodes.Ldarg_0)
            ))
        {
            LogHelper.Error($"Target 2 not found!");
            return;
        }
        c.RemoveRange(1);
        c.Emit(OpCodes.Ldc_I4, (int)Plugin.Instance.MaxGameSpeed.Value);

        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldc_R8),
            x => x.Match(OpCodes.Add),
            x => x.Match(OpCodes.Stloc_1),
            x => x.Match(OpCodes.Ldloc_1),
            x => x.Match(OpCodes.Ldloc_0)
            ))
        {
            LogHelper.Error($"Target 3 not found!");
            return;
        }
        c.RemoveRange(1);
        c.Emit(OpCodes.Ldc_R8, (double)Plugin.Instance.GameSpeedChangeIncrement.Value);

        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldc_R8),
            x => x.Match(OpCodes.Bgt_Un_S),
            x => x.Match(OpCodes.Ldloc_1),
            x => x.Match(OpCodes.Conv_I4)
            ))
        {
            LogHelper.Error($"Target 4 not found!");
            return;
        }
        c.RemoveRange(1);
        c.Emit(OpCodes.Ldc_R8, (double)Plugin.Instance.MaxGameSpeed.Value);

        LogHelper.Verbose($"IL={ctx.ToString()}");
    }

    internal ILHook? director_DecreaseFrameRate;
    internal void Director_DecreaseFrameRate_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldc_R8),
            x => x.Match(OpCodes.Ble_Un_S),
            x => x.Match(OpCodes.Ldloc_0),
            x => x.Match(OpCodes.Ldc_R8)
            ))
        {
            LogHelper.Error($"Target 1 not found!");
            return;
        }
        c.RemoveRange(1);
        c.Emit(OpCodes.Ldc_R8, (double)Plugin.Instance.MinGameSpeed.Value);

        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldc_R8),
            x => x.Match(OpCodes.Bge_Un_S),
            x => x.Match(OpCodes.Ldc_R8),
            x => x.Match(OpCodes.Stloc_0)
            ))
        {
            LogHelper.Error($"Target 2 not found!");
            return;
        }
        c.RemoveRange(1);
        c.Emit(OpCodes.Ldc_R8, (double)Plugin.Instance.MinGameSpeed.Value);

        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldc_R8),
            x => x.Match(OpCodes.Stloc_0),
            x => x.Match(OpCodes.Ldarg_0),
            x => x.Match(OpCodes.Ldloc_0)
            ))
        {
            LogHelper.Error($"Target 3 not found!");
            return;
        }
        c.RemoveRange(1);
        c.Emit(OpCodes.Ldc_R8, (double)Plugin.Instance.MinGameSpeed.Value);

        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldc_R8),
            x => x.Match(OpCodes.Bgt_Un_S)
            ))
        {
            LogHelper.Error($"Target 4 not found!");
            return;
        }
        c.RemoveRange(1);
        c.Emit(OpCodes.Ldc_R8, (double)Plugin.Instance.MaxGameSpeed.Value);

        c.Index = 0;
        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldc_R8),
            x => x.Match(OpCodes.Sub)
            ))
        {
            LogHelper.Error($"Target 5 not found!");
            return;
        }
        c.RemoveRange(1);
        c.Emit(OpCodes.Ldc_R8, (double)Plugin.Instance.GameSpeedChangeIncrement.Value);

        LogHelper.Verbose($"IL={ctx.ToString()}");
    }

    internal ILHook? director_SetEngineFrameRate;
    internal void Director_SetEngineFrameRate_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldc_I4_S),
            x => x.Match(OpCodes.Stloc_0),
            x => x.Match(OpCodes.Call),
            x => x.Match(OpCodes.Callvirt)
            ))
        {
            LogHelper.Error($"Target 1 not found!");
            return;
        }
        c.RemoveRange(1);
        c.Emit(OpCodes.Ldc_I4, Plugin.Instance.MaxGameSpeed.Value);

        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldc_I4),
            x => x.Match(OpCodes.Stloc_0),
            x => x.Match(OpCodes.Ldarg_1),
            x => x.Match(OpCodes.Ldc_R8)
            ))
        {
            LogHelper.Error($"Target 2 not found!");
            return;
        }
        c.RemoveRange(1);
        c.Emit(OpCodes.Ldc_I4, Plugin.Instance.MaxGameSpeed.Value);

        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldc_R8),
            x => x.Match(OpCodes.Blt_S),
            x => x.Match(OpCodes.Ldarg_1),
            x => x.Match(OpCodes.Ldloc_0)
            ))
        {
            LogHelper.Error($"Target 3 not found!");
            return;
        }
        c.RemoveRange(1);
        c.Emit(OpCodes.Ldc_R8, (double)Plugin.Instance.MinGameSpeed.Value);

        LogHelper.Verbose($"IL={ctx.ToString()}");
    }
}
