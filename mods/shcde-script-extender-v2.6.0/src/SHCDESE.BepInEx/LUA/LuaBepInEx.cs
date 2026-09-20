using Serilog;
using Serilog.Events;
using SHCDESE.API;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Lua.DocsGen;
using System;
using System.Numerics;
using BepInEx;
using BepInEx.Bootstrap;
using System.Linq;
using System.Collections.Generic;
using NLua;
using SHCDESE.Logging;
using SHCDESE.LUA.DocsGen;
namespace SHCDESE.Lua;

/// <summary>
/// Provides some bepinex functions to Lua.
/// </summary>
[LuaApiNamespace("BepInEx")]
public static class LuaBepInEx
{
    /// <summary>
    /// Registers the bepinex functions into the provided Lua state.
    /// </summary>
    /// <param name="lua">The <see cref="NLua.Lua"/> instance that the functions will be registered into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedStaticMethods(typeof(LuaBepInEx));
    }


    /// <summary>
    /// Returns all known bepinex mod GUIDs
    /// </summary>
    /// <example>
    /// <param name="onCompleteCallback">The function to execute once the mod GUIDs have been found</param>
    /// BepInEx_GetAll(function(mods)
    /// local n = mods.Length
    ///     for i = 1, n, 1 do
    ///         print(mods[i - 1])
    ///     end
    /// end);
    /// </example>
    [LuaApiExport("GetAll")]
    public static void GetAllGUIDs(LuaFunction onCompleteCallback)
    {
        UnityMainThreadDispatcher.DispatchGetAsync(() =>
        {
            List<string> names = new List<string>();
            foreach (string? pluginNames in Chainloader.PluginInfos.Keys)
            {
                PluginInfo pluginInfo = Chainloader.PluginInfos[pluginNames];
                names.Add(pluginInfo.Metadata.GUID);
            }
            return names.ToArray();
        }, onCompleteCallback);
    }

    /// <summary>
    /// Returns all dependencies of a bepinex GUID
    /// </summary>
    /// <param name="guid">The mod GUID</param>
    /// <param name="onCompleteCallback">The function to execute once the mod dependency GUIDs have been found</param>
    [LuaApiExport("GetDependencies")]
    public static void GetDependencies(string guid, LuaFunction onCompleteCallback)
    {
        UnityMainThreadDispatcher.DispatchGetAsync(() =>
        {
            if (!Chainloader.PluginInfos.TryGetValue(guid, out PluginInfo pluginInfo))
            {
                LogHelper.Error($"Could not find bepinex mod by GUID: [{guid}]");
                return null;
            }
            List<string> names = new List<string>();
            foreach (BepInDependency dep in pluginInfo.Dependencies)
            {
                names.Add(dep.DependencyGUID);
            }

            return names.ToArray();
        }, onCompleteCallback);
    }

}