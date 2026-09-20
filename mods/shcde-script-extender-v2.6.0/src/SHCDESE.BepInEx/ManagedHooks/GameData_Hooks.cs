using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Logging;
using System;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // GameData
    //
    internal ManagedDetour<gameData_getChimpGoldCost_delegate> gameData_getChimpGoldCost_hook;
    internal delegate int gameData_getChimpGoldCost_delegate(int troopChimpType);
    internal int GameData_getChimpGoldCosts_Hook(int troopChimpType)
    {
        try
        {
            return GameUnitManagerAPI.Instance.GetUnitGoldCost((eChimps)troopChimpType);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error");
            return gameData_getChimpGoldCost_hook.Trampoline(troopChimpType);
        }
    }
}
