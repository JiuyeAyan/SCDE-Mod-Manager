using CrusaderDE;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.GameGlobals;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using System;

namespace SHCDESE.ManagedHooks.ILHooks;

public partial class ILHooksManager
{
    //
    // SteamManager
    //
    internal ILHook? steamManager_Awake_ilhook;
    public void SteamManager_Awake_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        ILLabel skipLabel = c.DefineLabel();

        // Find jump target
        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldarg_0),
            x => x.Match(OpCodes.Call),
            x => x.Match(OpCodes.Stfld),
            x => x.Match(OpCodes.Ldarg_0),
            x => x.Match(OpCodes.Ldfld)))
        {
            LogHelper.Error($"Target 1 not found!");
            return;
        }
        c.MarkLabel(skipLabel);
        c.Index = 0;

        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Nop),
            x => x.Match(OpCodes.Ldsfld),
            x => x.Match(OpCodes.Call),
            x => x.Match(OpCodes.Brfalse_S)))
        {
            LogHelper.Error($"Target 2 not found!");
            return;
        }

        c.Emit(OpCodes.Br, skipLabel);

        LogHelper.Verbose($"IL={ctx.ToString()}");
    }
}
