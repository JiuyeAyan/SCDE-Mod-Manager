using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.AI;
using SHCDESE.Logging;
using System;
using System.IO;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // CustomisationFileManager
    //

    internal static ManagedDetour<customisationFileManager_ProcessExtendedLordFile_Delegate> customisationFileManager_ProcessExtendedLordFile_hook;
    internal delegate void customisationFileManager_ProcessExtendedLordFile_Delegate(CustomisationFileManager instance, string file, string realFileName, CustomisationFileManager.CustomLord customLord, bool workshop = false);
    internal void CustomisationFileManager_ProcessExtendedLordFile_Hook(CustomisationFileManager instance, string file, string realFileName, CustomisationFileManager.CustomLord customLord, bool workshop = false)
    {
        customisationFileManager_ProcessExtendedLordFile_hook.Trampoline(instance, file, realFileName, customLord, workshop);
        try
        {
            AIProcessCustomLordEventArgs eventArgs = new(EventHookPhase.Pre, customLord.customPath, customLord);
            AIR3EventHooks.OnAIProcessCustomLord.Raise(eventArgs);

            //  Processing Custom Lord:
            //  file=h:\steamlibrary\steamapps\workshop\content\3024040\3659458903\lord nox\info.json,
            //  realFileName=H:\SteamLibrary\steamapps\workshop\content\3024040\3659458903\Lord Nox\info.json,
            //  customPath=H:\SteamLibrary\steamapps\workshop\content\3024040\3659458903\Lord Nox,
            //  displayName=Lord Nox,
            //  lordName=3659458903\Lord Nox,
            //  workshop=False
            if (Path.GetFileName(file).Equals("info.json", StringComparison.InvariantCultureIgnoreCase))
            {
                LogHelper.Information($"Processing Custom Lord: file={file}, realFileName={realFileName}, customPath={customLord.customPath}, displayName={customLord.lordDisplayName}, lordName={customLord.lordName}, workshop={workshop}");

                GameAIManagerAPI.Instance.ProcessCustomLord(customLord.customPath, customLord);

                AIProcessCustomLordEventArgs postEventArgs = new(EventHookPhase.Post, customLord.customPath, customLord);
                AIR3EventHooks.OnAIProcessCustomLord.Raise(postEventArgs);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception during hide event");
        }
    }

    internal static ManagedDetour<customisationFileManager_GetCustomLordText_Delegate> customisationFileManager_GetCustomLordText_hook;
    internal delegate string customisationFileManager_GetCustomLordText_Delegate(CustomisationFileManager instance, string lordName, int ID);
    internal string CustomisationFileManager_GetCustomLordText_Hook(CustomisationFileManager instance, string lordName, int ID)
    {
        string ret = customisationFileManager_GetCustomLordText_hook.Trampoline(instance, lordName, ID);
        try
        {
            GameAIManagerAPI aiApi = GameAIManagerAPI.Instance;
            if (aiApi.IsSupportedCustomLord(lordName))
            {
                if (aiApi.TryGetLordTitle(lordName, out string? newText, ID - 34))
                {
                    int index = newText.IndexOf(',');
                    if (index >= 0)
                        newText = newText.Remove(index, 1);
                    ret = newText;
                }
            }

            LogHelper.Verbose($"Ret: {ret}, lordName={lordName}, ID={ID}, slot={ID - 34}");
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception during get custom lord text");
        }
        return ret;
    }


}
