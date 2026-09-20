using NLua;
using SHCDESE.API;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using SHCDESE.Lua.CodeGen;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SHCDESE.Lua;

/// <summary>
/// Exposes the GameTribeManagerAPI functionality to the Lua scripting environment.
/// </summary>
[LuaApiNamespace("Tribe")]
public static unsafe class LuaTribeAPI
{
    /// <summary>
    /// Registers all tribe-related functions and helper methods with the specified Lua instance.
    /// </summary>
    /// <param name="lua">The Lua state to register the functions into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedMethods(GameTribeManagerAPI.Instance);
        lua.RegisterExportedStaticMethods(typeof(LuaTribeAPI));

        // --- Metatable Helpers ---
        lua.DoString(@"function Tribe_CreateInterface(id)
                    return setmetatable({id = id}, {
                        __index = function(t, k)
                            return Tribe_GetField(t.id, k)
                        end,
                        __newindex = function(t, k, v)
                            Tribe_SetField(t.id, k, v)
                        end
                    })
                end", "lua_tribeapi");
    }

    /// <summary>
    /// Gets the value of a field from a tribe's data structure. Exposed to Lua for metatable use.
    /// </summary>
    /// <param name="id">The ID of the tribe.</param>
    /// <param name="field">The name of the field to retrieve.</param>
    /// <returns>The value of the field, or null if the tribe or field is not found or not exposed.</returns>
    [LuaApiExport("GetField")]
    public static object GetTribeField(int id, string field)
    {
        if (!GameTribeManagerAPI.Instance.TryGetTribeById(id, out GameTribe* ptr))
        {
            LogHelper.Warning($"Could not find tribe by id: {id}");
            return null;
        }
        FieldInfo info = typeof(GameTribe).GetField(field, BindingFlags.Public | BindingFlags.Instance);
        if (info == null || !Attribute.IsDefined(info, typeof(LuaExposedAttribute))) 
        {
            LogHelper.Warning($"Could not find field: {field} (or not exposed)");
            return null;
        }

        TypedReference tr = __makeref(*ptr);
        return info.GetValueDirect(tr);
    }

    /// <summary>
    /// Sets the value of a field in a tribe's data structure. Exposed to Lua for metatable use.
    /// </summary>
    /// <param name="id">The ID of the tribe.</param>
    /// <param name="field">The name of the field to modify.</param>
    /// <param name="value">The new value to set.</param>
    [LuaApiExport("SetField")]
    public static void SetTribeField(int id, string field, object value)
    {
        if (!GameTribeManagerAPI.Instance.TryGetTribeById(id, out GameTribe* tribe))
        {
            LogHelper.Warning($"Could not find tribe by id: {id}");
            return;
        }
        StructFieldSetter.SetField(tribe, field, value, typeof(LuaExposedAttribute));
    }

    /// <summary>
    /// A Lua-friendly wrapper for setting a tribe's patrol path from a Lua table.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <param name="pathTable">A Lua table (array) of UnmanagedVector2_UInt16 points defining the path.</param>
    /// <param name="patrolMode">The mode of patrol (e.g., infinite, once).</param>
    /// <param name="startIndex">The index in the path table where the patrol should begin.</param>
    /// <param name="moveType">The movement type the tribe should use while patrolling.</param>
    /// <returns><c>true</c> if the path was successfully set; otherwise, <c>false</c>.</returns>
    /// <example>
    /// <code>
    /// local path = { Vec2UShort(100, 100), Vec2UShort(120, 100), Vec2UShort(120, 120) }
    /// Tribe_SetPatrolPath(myTribeId, path, eTribePatrolMode.PatrolInfinite, 0)
    /// </code>
    /// </example>
    [LuaApiExport("SetPatrolPath")]
    public static bool SetPatrolPathLua(int tribeId, LuaTable pathTable, TribePatrolMode patrolMode = TribePatrolMode.PatrolInfinite, int startIndex = 0, TribeMoveType moveType = TribeMoveType.DefaultInSync)
    {
        List<UnmanagedVector2<UInt16>> list = pathTable.ToList<UnmanagedVector2<UInt16>>();
        return GameTribeManagerAPI.Instance.SetPatrolPath(tribeId, list.ToArray(), patrolMode, startIndex, moveType);
    }

    /// <summary>
    /// A Lua-friendly wrapper to get a tribe's global ID.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <returns>The tribe's global ID as an integer, or 0 if the tribe is not found.</returns>
    [LuaApiExport("GetGlobalId")]
    public static int GetGlobalIdOfTribe(int tribeId) 
    {
        if (!GameTribeManagerAPI.Instance.TryGetGlobalId(tribeId, out uint globalId)) 
        {
            return 0;
        }
        return (int)globalId;
    }

    /// <summary>
    /// A Lua-friendly wrapper to get the unit ids of a tribe.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <returns>The tribe's unit ids.</returns>
    [LuaApiExport("GetUnits")]
    public static int[] GetUnits(int tribeId)
    {
        List<int> result = new List<int>();
        GameTribeManagerAPI.Instance.GetUnits(tribeId, result);
        return [.. result];
    }
}
