using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using SHCDESE.EventAPI;
using SHCDESE.Logging;

namespace SHCDESE.ManagedHooks.ILHooks;

public partial class ILHooksManager
{
    //
    // GameMap
    //
    internal ILHook? gameMap_AddUpdateChimp;
    internal void GameMap_AddUpdateChimp_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);
        if (!c.TryGotoNext(MoveType.After,
            x => x.Match(OpCodes.Call),
            x => x.Match(OpCodes.Brfalse),
            x => x.Match(OpCodes.Ldloc_S),
            x => x.Match(OpCodes.Callvirt)
            ))
        {
            LogHelper.Error($"Target not found!");
            return;
        }

        c.Emit(OpCodes.Dup);     // SpriteRenderer
        c.Emit(OpCodes.Ldarg_1); // UnitId (objectId)
        c.EmitDelegate(static (UnityEngine.SpriteRenderer sr, int unitId) =>
        {
            LogHelper.Verbose($"Adding chimp unity-side: {unitId}");
            UnitR3EventHooks.OnUnitUnityVisualSpawn.Raise(new EventAPI.Units.UnitUnityVisualSpawnEventArgs(EventHookPhase.Pre, unitId, sr.gameObject, sr));
        });
        LogHelper.Verbose($"IL={ctx.ToString()}");
    }

}
