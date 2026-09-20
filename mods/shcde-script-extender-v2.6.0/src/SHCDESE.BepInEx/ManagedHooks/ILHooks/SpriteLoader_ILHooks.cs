using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Logging;
using System;

namespace SHCDESE.ManagedHooks.ILHooks;

public partial class ILHooksManager
{
    //
    // This hook makes it possible for mods to override existing unity engine atlas sprites (non-noesis ones)
    //
    internal ILHook? spriteLoader_LoadSprites;
    public void SpriteLoader_LoadSprites_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        if (!c.TryGotoNext(MoveType.After,
            x => x.Match(OpCodes.Ldloc_1),
            x => x.Match(OpCodes.Ldc_I4_1),
            x => x.Match(OpCodes.Stfld)))
        {
            LogHelper.Error($"Target 1 not found!");
            return;
        }

        c.Emit(OpCodes.Ldloc_1);
        c.EmitDelegate(static (spriteLoader instance) =>
        {
            try
            {
                GameSpriteManagerAPI.Instance.ApplyRuntimeOverrides(instance);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "Exception during custom sprite loading");
            }
        });

        LogHelper.Verbose($"IL={ctx.ToString()}");
    }
}
