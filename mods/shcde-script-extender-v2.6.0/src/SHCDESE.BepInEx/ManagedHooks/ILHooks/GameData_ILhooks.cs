using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using SHCDESE.GameGlobals;
using SHCDESE.Logging;
namespace SHCDESE.ManagedHooks.ILHooks;

public partial class ILHooksManager
{
    //
    // GameData: setGameState
    //
    internal ILHook? gameData_setGameStatew_ilhook;
    public void gameData_setGameState_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldc_I4, 7000),
            x => x.Match(OpCodes.Div),
            x => x.Match(OpCodes.Call)))
        {
            LogHelper.Error($"Target not found!");
            return;
        }

        c.Remove();
        c.EmitDelegate(static () =>
        {
            if (GameGlobalsManager.Instance.MaxMana == null)
            {
                LogHelper.Verbose($"GameGlobalsManager.Instance.MaxMana is null (this can be fine)");
                return 7000;
            }

            return (int)GameGlobalsManager.Instance.MaxMana.GetValue();
        });

        LogHelper.Verbose($"IL={ctx.ToString()}");
    }

}
