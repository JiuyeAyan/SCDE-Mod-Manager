using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Logging;

namespace SHCDESE.ManagedHooks.ILHooks;

public partial class ILHooksManager
{
    internal ILHook? mapFileManager_mapFleSizeLimit;
    internal void MapFileManager_GetFileInfoFromFileName_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        int startIndex = 0;
        int endIndex = 0;
        int count = 0;
        int it = 0;
        //LogHelper.Debug($"IL ={ctx.ToString()}");

        if (!c.TryGotoNext(MoveType.Before, 
            x => x.MatchLdloc(7),
            x => x.Match(OpCodes.Callvirt),
            x => x.MatchLdcI4(0x88B8),
            x => x.MatchConvI8(),
            x => x.Match(OpCodes.Blt_S),
            x => x.MatchLdloc(7),
            x => x.Match(OpCodes.Callvirt),
            x => x.MatchLdcI4(0x895440)))
        {
            LogHelper.Error($"Failed to find target 1!");
            return;
        }
        c.Index += 2;
        c.Remove();
        c.Emit(OpCodes.Ldc_I4, GameMapArchiveManagerAPI.DEFAULT_MINIMUM_MAP_FILE_SIZE);

        c.Index += 5;
        c.Remove();
        c.Emit(OpCodes.Ldc_I4, GameMapArchiveManagerAPI.DEFAULT_MAXIMUM_MAP_FILE_SIZE);

        LogHelper.Verbose($"IL={ctx.ToString()}");
    }
}
