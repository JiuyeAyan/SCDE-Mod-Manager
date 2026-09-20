using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.Logging;
using System;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // GameMap
    //
    internal static ManagedDetour<GameMap_InterpChimpDelegate> gameMap_interpChimp_hook;
    internal delegate void GameMap_InterpChimpDelegate(GameMap instance, Chimp this_chimp, bool force = false);
    internal void GameMap_InterpChimpHook(GameMap instance, Chimp this_chimp, bool force = false)
    {
        try
        {
            UnitR3EventHooks.OnUnitUnityVisualInterpolate.Raise(new EventAPI.Units.UnitUnityVisualInterpolateEventArgs(EventHookPhase.Pre, this_chimp));
        } 
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error while running interpChimp");
        }
        gameMap_interpChimp_hook.Trampoline(instance, this_chimp, force);
    }

    internal static ManagedDetour<GameMap_DeleteChimpDelegate> gameMap_deleteChimp_hook;
    internal delegate void GameMap_DeleteChimpDelegate(GameMap instance, Chimp this_chimp, bool removeFromDictionary = false);
    internal void GameMap_DeleteChimpHook(GameMap instance, Chimp this_chimp, bool removeFromDictionary = false)
    {
        try
        {
            UnitR3EventHooks.OnUnitUnityVisualRemove.Raise(new EventAPI.Units.UnitUnityVisualRemoveEventArgs(EventHookPhase.Pre, this_chimp));
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error while running deleteChimp");
        }
        gameMap_deleteChimp_hook.Trampoline(instance, this_chimp, removeFromDictionary);
    }

}
