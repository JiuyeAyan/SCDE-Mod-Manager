using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.Logging;

namespace SHCDESE.ManagedHooks.ILHooks;

public partial class ILHooksManager
{
    //
    // ConfigSettings
    //
    internal ILHook? configSettings_maxGameSpeed;
    internal void ConfigSettings_MaxGameSpeed_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);
        if (!c.TryGotoNext(
            x => x.Match(OpCodes.Ldc_I4_5),
            x => x.Match(OpCodes.Mul),
            x => x.Match(OpCodes.Ldc_I4_S),
            x => x.Match(OpCodes.Ldc_I4_S),
            x => x.Match(OpCodes.Call),
            x => x.Match(OpCodes.Stsfld)
            ))
        {
            LogHelper.Error($"Target not found!");
            return;
        }
        c.Index += 2;

        // ldc.i4.s 10
        // ldc.i4.s 0x5A
        c.RemoveRange(2);
        c.Emit(OpCodes.Ldc_I4, (int)Plugin.Instance.MinGameSpeed.Value);
        c.Emit(OpCodes.Ldc_I4, (int)Plugin.Instance.MaxGameSpeed.Value);

        LogHelper.Verbose($"IL={ctx.ToString()}");
    }

}
