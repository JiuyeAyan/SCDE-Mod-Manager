using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using SHCDESE.Logging;

namespace SHCDESE.ManagedHooks.ILHooks;

public partial class ILHooksManager
{
    //
    // KeyManager: ILhook to enable the chat in non-multiplayer matches
    //
    internal ILHook? keyManager_update_ilhook;
    public void KeyManager_Update_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldsfld),
            x => x.Match(OpCodes.Callvirt),
            x => x.Match(OpCodes.Brfalse_S),
            x => x.Match(OpCodes.Call),
            x => x.Match(OpCodes.Ldfld),
            x => x.Match(OpCodes.Callvirt)))
        {
            LogHelper.Error($"Target 1 not found!");
            return;
        }
        c.RemoveRange(2);
        c.Emit(OpCodes.Nop);
        c.Emit(OpCodes.Ldc_I4_1);

        LogHelper.Verbose($"IL={ctx.ToString()}");
    }

}
