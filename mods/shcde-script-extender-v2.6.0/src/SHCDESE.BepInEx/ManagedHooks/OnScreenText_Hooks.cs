using SHCDESE.API;
using SHCDESE.Logging;
using System;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // OnScreenText
    //

    internal static ManagedDetour<onScreenText_GetComputerName_Delegate> onScreenText_GetComputerName_hook;
    internal delegate string onScreenText_GetComputerName_Delegate(int computerOpponent, int computerName);
    internal static string OnScreenText_GetComputerName_Hook(int computerOpponent, int computerName)
    {
        try
        {
            LogHelper.Verbose($"computerOpponent={computerOpponent}, computerName={computerName}");

            GameAIManagerAPI aiApi = GameAIManagerAPI.Instance;

            Enums.AILords lord = (Enums.AILords)computerOpponent;
            if (!aiApi.GetSlotIndexByExtendedLordEnum(lord, out int internalLordId))
                return onScreenText_GetComputerName_hook.Trampoline(computerOpponent, computerName);

            string lordName = aiApi.GetGenericSlotLordName(internalLordId).ToLowerInvariant();

            if (internalLordId == -1 || !aiApi.IsSupportedCustomLord(lordName))
                return onScreenText_GetComputerName_hook.Trampoline(computerOpponent, computerName);

            LogHelper.Verbose($"internalLordId: {internalLordId}, Lord: {lord}, lordName={lordName}");
            if (!aiApi.TryGetDisplayName(lordName, out string? displayName))
                return onScreenText_GetComputerName_hook.Trampoline(computerOpponent, computerName);

            if (!aiApi.TryGetLordTitle(lordName, out string? lordTitle, internalLordId))
                return onScreenText_GetComputerName_hook.Trampoline(computerOpponent, computerName);

            return displayName + lordTitle;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error during retrieval attempt");
        }
        return onScreenText_GetComputerName_hook.Trampoline(computerOpponent, computerName);
    }
}
