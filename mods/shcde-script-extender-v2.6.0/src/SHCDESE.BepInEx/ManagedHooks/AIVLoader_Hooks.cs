using SHCDESE.API;
using SHCDESE.Logging;
using System;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    internal delegate short[] AIVLoader_GetAIVData_Delegate(int lord, int id, bool evreySkirmishSet = false, bool evreyHistoricalSet = false);
    internal static ManagedDetour<AIVLoader_GetAIVData_Delegate> aivloader_GetAIVData_hook;

    /// <summary>
    /// Responsible for retrieving AIVdata which is embedded in the game.
    /// lord = Enums.AILords + 1
    /// id = aiv index
    /// Gets used as a point to override AICs.
    /// </summary>
    /// <param name="lord">The lord + 1</param>
    /// <param name="id">The index that describes the lord's AIV</param>
    /// <param name="evreySkirmishSet">Undocumented</param>
    /// <param name="evreyHistoricalSet">Undocumented</param>
    internal static short[] AIVLoader_GetAIVData_Hook(int lord, int id, bool evreySkirmishSet = false, bool evreyHistoricalSet = false)
    {
        try
        {
            Enums.AILords lordEnum = (Enums.AILords)(lord + 1);
            LogHelper.Verbose($"Intercepting load of lord: [{lordEnum}], index={id}");
            string aivOverridePath = $"AIV/{lordEnum}_{id}.aivjson";
            string aicOverridePath = $"AIC/{lordEnum}.baic";

            if (GameAssetManagerAPI.Instance.GetFileBinaryContent(aicOverridePath, out byte[] baic))
            {
                LogHelper.Information($"Overriding AIC of lord: [{lordEnum}]");
                GameAIManagerAPI.Instance.SetAICFromBytes(lordEnum, baic);
            }

            if (!GameAssetManagerAPI.Instance.GetModifiedFileTextContent(aivOverridePath, out string aivjson))
            {
                LogHelper.Verbose($"No override found at [{aivOverridePath}]");
                return aivloader_GetAIVData_hook.Trampoline(lord, id, evreySkirmishSet, evreyHistoricalSet);
            }

            AIVLoader.SaveData? aivOverrideSaveData = UnityEngine.JsonUtility.FromJson<AIVLoader.SaveData>(aivjson);
            if (aivOverrideSaveData == null)
            {
                LogHelper.Warning($"Could not load json override at [{aivOverridePath}] - malformed?");
                return aivloader_GetAIVData_hook.Trampoline(lord, id, evreySkirmishSet, evreyHistoricalSet);
            }
            short[] aivOverrideInternal = aivOverrideSaveData.GetRawData();
            LogHelper.Information($"Intercepting load of lord: [{lordEnum}], index={id} - overriden");
            return aivOverrideInternal;

        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception during aivdata retrieval.");
        }
        return aivloader_GetAIVData_hook.Trampoline(lord, id, evreySkirmishSet, evreyHistoricalSet);
    }
}
