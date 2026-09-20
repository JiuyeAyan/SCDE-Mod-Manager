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
using UnityEngine;

namespace SHCDESE.Lua;

/// <summary>
/// Exposes the GameVegetationAPI functionality to the Lua scripting environment.
/// </summary>
[LuaApiNamespace("Vegetation")]
public unsafe static class LuaVegetationAPI
{
    /// <summary>
    /// Registers all vegetation-related functions with the specified Lua instance.
    /// </summary>
    /// <param name="lua">The Lua state to register the functions into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedMethods(GameVegetationManagerAPI.Instance);
        lua.RegisterExportedStaticMethods(typeof(LuaVegetationAPI));

        // Create metatable for GameVegetation
        lua.DoString(@"function Vegetation_CreateInterface(id)
                    return setmetatable({id = id}, {
                        __index = function(t, k)
                            return Vegetation_GetField(t.id, k)
                        end,
                        __newindex = function(t, k, v)
                            Vegetation_SetField(t.id, k, v)
                        end
                    })
                end", "lua_vegetationapi");
    }

    /// <summary>
    /// Gets the value of a field from a vegetation object's data structure. Exposed to Lua for metatable use.
    /// </summary>
    /// <param name="id">The ID of the vegetation.</param>
    /// <param name="field">The name of the field to retrieve.</param>
    /// <returns>The value of the field, or null if the object or field is not found or not exposed.</returns>
    [LuaApiExport("GetField")]
    public static object GetVegetationField(int id, string field)
    {
        if (!GameVegetationManagerAPI.Instance.TryGetVegetationById(id, out GameVegetation* ptr))
        {
            LogHelper.Warning($"Could not find vegetation by id: {id}");
            return null;
        }
        FieldInfo info = typeof(GameVegetation).GetField(field, BindingFlags.Public | BindingFlags.Instance);
        if (info == null || !Attribute.IsDefined(info, typeof(LuaExposedAttribute)))
        {
            LogHelper.Warning($"Could not find field: {field} (or not exposed)");
            return null;
        }

        TypedReference tr = __makeref(*ptr);
        return info.GetValueDirect(tr);
    }

    /// <summary>
    /// Sets the value of a field in a vegetation object's data structure. Exposed to Lua for metatable use.
    /// </summary>
    /// <param name="id">The ID of the vegetation.</param>
    /// <param name="field">The name of the field to modify.</param>
    /// <param name="value">The new value to set.</param>
    [LuaApiExport("SetField")]
    public static void SetVegetationField(int id, string field, object value)
    {
        if (!GameVegetationManagerAPI.Instance.TryGetVegetationById(id, out GameVegetation* ptr))
        {
            LogHelper.Warning($"Could not find vegetation by id: {id}");
            return;
        }
        StructFieldSetter.SetField(ptr, field, value, typeof(LuaExposedAttribute));
    }

    /// <summary>
    /// Finds all vegetation IDs within a rectangular area, with optional filters.
    /// </summary>
    /// <param name="options">A Lua table containing query parameters: x, y, width, height, and optional 'state' and 'type'.</param>
    /// <returns>An array of one-based vegetation IDs that match the criteria. Each value can be passed
    /// directly to any <c>Vegetation_*</c> function that takes a vegetationId.</returns>
    /// <example>
    /// <code>
    /// local trees = Vegetation_FindInRect({ x=10, y=10, width=20, height=20, type=eVegetationType.Tree_Oak })
    /// </code>
    /// </example>
    [LuaApiExport("FindInRect")]
    public static int[] GetVegetationInRect(LuaTable options)
    {
        // Required spatial parameters
        int x = Convert.ToInt32(options["x"]);
        int y = Convert.ToInt32(options["y"]);
        int width = Convert.ToInt32(options["width"]);
        int height = Convert.ToInt32(options["height"]);

        // Optional filter parameters
        ParseOptions(options, out AliveState? state, out VegetationType? type);

        List<int> result = new List<int>();
        GameVegetationManagerAPI.Instance.ExecuteQuery(result, GameVegetationManagerAPI.VegetationPredicates.IsWithinRect(x, y, width, height), state, type);
        return [.. result];
    }

    /// <summary>
    /// Finds all vegetation IDs within a spherical area, with optional filters.
    /// </summary>
    /// <param name="options">A Lua table containing query parameters: x, y, radius, and optional 'state' and 'type'.</param>
    /// <returns>An array of one-based vegetation IDs that match the criteria. Each value can be passed
    /// directly to any <c>Vegetation_*</c> function that takes a vegetationId.</returns>
    /// <example>
    /// <code>
    /// local shrubs = Vegetation_FindInSphere({ x=50, y=50, radius=10, type=eVegetationType.Shrub })
    /// </code>
    /// </example>
    [LuaApiExport("FindInSphere")]
    public static int[] GetVegetationInSphere(LuaTable options)
    {
        // Required spatial parameters
        int x = Convert.ToInt32(options["x"]);
        int y = Convert.ToInt32(options["y"]);
        int radius = Convert.ToInt32(options["radius"]);

        // Optional filter parameters
        ParseOptions(options, out AliveState? state, out VegetationType? type);

        List<int> result = new List<int>();
        GameVegetationManagerAPI.Instance.ExecuteQuery(result, GameVegetationManagerAPI.VegetationPredicates.IsWithinSphere(x, y, radius), state, type);
        return [.. result];
    }

    /// <summary>
    /// Finds all vegetation IDs on the map, with optional filters.
    /// </summary>
    /// <param name="options">Optional. A Lua table containing query parameters: 'state' and 'type'.</param>
    /// <returns>An array of one-based vegetation IDs that match the criteria. Each value can be passed
    /// directly to any <c>Vegetation_*</c> function that takes a vegetationId.</returns>
    /// <example>
    /// <code>
    /// local allDeadTrees = Vegetation_GetAllIds({
    ///     state = eAliveState.MarkedForDeletion,
    ///     type = eVegetationType.Tree_Apple
    /// })
    /// </code>
    /// </example>
    [LuaApiExport("GetAllIds")]
    public static int[] GetAllVegetationIds(LuaTable options = null)
    {
        ParseOptions(options, out AliveState? state, out VegetationType? type);

        List<int> result = new List<int>();
        GameVegetationManagerAPI.Instance.GetAllVegetation(result, state, type);
        return [.. result];
    }

    /// <summary>
    /// Helper to parse common options from the Lua table.
    /// </summary>
    private static void ParseOptions(LuaTable options, out AliveState? state, out VegetationType? type)
    {
        // Default values
        state = null;
        type = null;

        if (options == null)
        {
            return;
        }

        // Parse state filter (e.g., state = eAliveState.IsAlive)
        if (options["state"] is AliveState parsedState)
        {
            state = parsedState;
        }

        // Parse type filter (e.g., type = eVegetationType.Tree_Oak)
        if (options["type"] is VegetationType parsedType)
        {
            type = parsedType;
        }
    }
}
