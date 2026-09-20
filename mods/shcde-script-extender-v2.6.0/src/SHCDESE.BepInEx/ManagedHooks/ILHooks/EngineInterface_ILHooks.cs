using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using SHCDESE.GameGlobals;
using SHCDESE.Logging;

namespace SHCDESE.ManagedHooks.ILHooks;

public partial class ILHooksManager
{
    //
    // EngineInterface
    //

    internal ILHook? engineInterface_CopyPlayStateStruct_ilhook;

    /// <summary>
    /// This hook allows being able to track from which playerId certain messages are.
    /// </summary>
    /// <param name="ctx"></param>
    internal void EngineInterface_CopyPlayStateStruct_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        if (!c.TryGotoNext(MoveType.Before,
            x => x.MatchStfld<EngineInterface.PlayState>(nameof(EngineInterface.PlayState.messageFromcharacter)),
            x => x.Match(OpCodes.Ldloc_0),
            x => x.Match(OpCodes.Ldarg_0),
            x => x.Match(OpCodes.Ldfld),
            x => x.Match(OpCodes.Stfld)
            ))
        {
            LogHelper.Error($"Target 1 not found!");
            return;
        }
        c.Emit(OpCodes.Dup);
        c.EmitDelegate(static (byte messageFromcharacter) =>
        {
            if (messageFromcharacter > 0 && messageFromcharacter < 9)
            {
                GameGlobalsManager.LastMessageFromCharacterCached = messageFromcharacter;
            }
        });
        LogHelper.Verbose($"IL={ctx.ToString()}");
    }
}
