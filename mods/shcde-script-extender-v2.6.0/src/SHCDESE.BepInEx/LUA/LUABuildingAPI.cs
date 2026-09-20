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
/// Exposes the GameBuildingManagerAPI functionality to the Lua scripting environment.
/// </summary>
[LuaApiNamespace("Building")]
public unsafe static class LuaBuildingAPI
{
    /// <summary>
    /// Registers all building-related functions and helper methods with the specified Lua instance.
    /// </summary>
    /// <param name="lua">The Lua state to register the functions into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedMethods(GameBuildingManagerAPI.Instance);
        lua.RegisterExportedStaticMethods(typeof(LuaBuildingAPI));

        // --- Metatable Helpers ---
        lua.DoString(@"function Building_CreateInterface(id)
                    return setmetatable({id = id}, {
                        __index = function(t, k)
                            return Building_GetField(t.id, k)
                        end,
                        __newindex = function(t, k, v)
                            Building_SetField(t.id, k, v)
                        end
                    })
                end", "lua_buildingapi");
    }

    /// <summary>
    /// Sets the default resource costs for the specified building type.
    /// LUA compatible version.
    /// </summary>
    /// <param name="building">The building type for which to set the default costs.</param>
    /// <param name="wood">The amount of wood required as the default cost.</param>
    /// <param name="stone">The amount of stone required as the default cost.</param>
    /// <param name="iron">The amount of iron required as the default cost.</param>
    /// <param name="pitch">The amount of pitch required as the default cost.</param>
    /// <param name="gold">The amount of gold required as the default cost.</param>
    [LuaApiExport("SetDefaultCost")]
    public static void SetBuildingDefaultCost(eStructs building, int wood, int stone, int iron, int pitch, int gold)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            GameBuildingManagerAPI.Instance.SetDefaultCost(building, new BuildingCost() { Wood = wood, Stone = stone, Iron = iron, Pitch = pitch, Gold = gold });
        });
    }

    /// <summary>
    /// Pops the default (maximum) health for a specific building type.
    /// </summary>
    /// <param name="building">The type of building (<see cref="eStructs"/>) to modify.</param>
    [LuaApiExport("PopDefaultHealth")]
    public static UInt32 PopDefaultHealth(eStructs building) => GameBuildingManagerAPI.Instance._buildingHealthDefaultsArray.Pop((int)building);

    /// <summary>
    /// Pushes the default (maximum) health for a specific building type.
    /// </summary>
    /// <param name="building">The type of building (<see cref="eStructs"/>) to modify.</param>
    /// <param name="value">The new default health value to set.</param>
    [LuaApiExport("PushDefaultHealth")]
    public static void PushDefaultHealth(eStructs building, UInt32 value) => GameBuildingManagerAPI.Instance._buildingHealthDefaultsArray.Push((int)building, value);
    
    /// <summary>
    /// Pops the default amount of population space provided by a housing building.
    /// </summary>
    /// <param name="building">The type of housing building (<see cref="eStructs"/>).</param>
    [LuaApiExport("PopDefaultHousingPopulationSpace")]
    public static ushort PopDefaultHousingPopulationSpace(eStructs building) => GameBuildingManagerAPI.Instance._housingPopulationSpaceDefaultsArray.Pop((int)building * 2);
    
    /// <summary>
    /// Pushes the default amount of population space provided by a housing building.
    /// </summary>
    /// <param name="building">The type of housing building (<see cref="eStructs"/>).</param>
    /// <param name="value">The new number of peasants the building should house.</param>
    [LuaApiExport("PushDefaultHousingPopulationSpace")]
    public static void PushDefaultHousingPopulationSpace(eStructs building, UInt16 value) => GameBuildingManagerAPI.Instance._housingPopulationSpaceDefaultsArray.Push((int)building * 2, value);
    
    /// <summary>
    /// Pops the building fire damage.
    /// </summary>
    /// <param name="building">The building</param>
    /// <returns>Fire damage to building</returns>
    [LuaApiExport("PopFireDamage")]
    public static Int16 PopBuildingFireDamage(eStructs building) => GameBuildingManagerAPI.PopBuildingFireDamageInternal(building);

    /// <summary>
    /// Pushes the building fire damage.
    /// 1 = Default for almost all buildings.
    /// </summary>
    /// <param name="building">The building</param>
    /// <param name="damage">The damage</param>
    [LuaApiExport("PushFireDamage")]
    public static void PushBuildingFireDamage(eStructs building, Int16 damage) => GameBuildingManagerAPI.PushBuildingFireDamageInternal(building, damage);

    /// <summary>
    /// Gets the default building scale of a specific building/object, given the eMappers value.
    /// </summary>
    /// <returns>The default building scale.</returns>
    [LuaApiExport("GetDefaultBuildingScale")]
    public static int GetDefaultBuildingScale(eMappers mv) => BuildingScales.GetScale(mv);

    /// <summary>
    /// Gets the value of a field from a building's data structure.
    /// </summary>
    /// <param name="id">The ID of the building.</param>
    /// <param name="field">The name of the field to retrieve.</param>
    /// <returns>The value of the field, or null if the building or field is not found or not exposed.</returns>
    [LuaApiExport("GetField")]
    public static object GetBuildingField(int id, string field)
    {
        if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(id, out GameBuilding* ptr))
        {
            LogHelper.Warning($"Could not find building by id: {id}");
            return null;
        }
        FieldInfo info = typeof(GameBuilding).GetField(field, BindingFlags.Public | BindingFlags.Instance);
        if (info == null || !Attribute.IsDefined(info, typeof(LuaExposedAttribute)))
        {
            LogHelper.Warning($"Could not find field by id: {field} (or not exposed)");
            return null;
        }
        TypedReference tr = __makeref(*ptr);
        return info.GetValueDirect(tr);
    }

    /// <summary>
    /// Sets the value of a field in a building's data structure.
    /// </summary>
    /// <param name="id">The ID of the building.</param>
    /// <param name="field">The name of the field to modify.</param>
    /// <param name="value">The new value to set.</param>
    [LuaApiExport("SetField")]
    public static void SetBuildingField(int id, string field, object value)
    {
        if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(id, out GameBuilding* ptr))
        {
            LogHelper.Warning($"Could not find building by id: {id}");
            return;
        }
        StructFieldSetter.SetField(ptr, field, value, typeof(LuaExposedAttribute));
    }

    /// <summary>
    /// Finds all building IDs within a rectangular area, with optional filters.
    /// </summary>
    /// <param name="options">A Lua table containing query parameters: x, y, width, height, and optional 'state', 'relationship', and 'povPlayer'.</param>
    /// <returns>An array of one-based building IDs that match the criteria. Each value can be passed
    /// directly to any <c>Building_*</c> function that takes a buildingId.</returns>
    /// <example>
    /// <code>
    /// local enemysBuildings = Building_FindInRect({ x=100, y=100, width=50, height=50, relationship=ePlayerRelationship.Enemy, povPlayer=1 })
    /// </code>
    /// </example>
    [LuaApiExport("FindInRect")]
    public static int[] GetUnitsInRect(LuaTable options)
    {
        // Required spatial parameters
        int x = Convert.ToInt32(options["x"]);
        int y = Convert.ToInt32(options["y"]);
        int width = Convert.ToInt32(options["width"]);
        int height = Convert.ToInt32(options["height"]);

        // Optional filter parameters
        ParseOptions(options, out AliveState? state, out eStructs? buildingType, out PlayerRelationship? relationship, out int? povPlayerId);

        List<int> result = new List<int>();
        GameBuildingManagerAPI.Instance.ExecuteQuery(result, GameBuildingManagerAPI.BuildingPredicates.IsWithinRect(x, y, width, height), state, buildingType, relationship, povPlayerId);
        return [.. result];
    }

    /// <summary>
    /// Finds all building IDs within a spherical area, with optional filters.
    /// </summary>
    /// <param name="options">A Lua table containing query parameters: x, y, radius, and optional 'state', 'relationship', and 'povPlayer'.</param>
    /// <returns>An array of one-based building IDs that match the criteria. Each value can be passed
    /// directly to any <c>Building_*</c> function that takes a buildingId.</returns>
    /// <example>
    /// <code>
    /// local alliedBuildings = Building_FindInSphere({ x=100, y=100, radius=20, relationship=ePlayerRelationship.Allied, povPlayer=1 })
    /// </code>
    /// </example>
    [LuaApiExport("FindInSphere")]
    public static int[] GetBuildingsInSphere(LuaTable options)
    {
        // Required spatial parameters
        int x = Convert.ToInt32(options["x"]);
        int y = Convert.ToInt32(options["y"]);
        int radius = Convert.ToInt32(options["radius"]);

        // Optional filter parameters
        ParseOptions(options, out AliveState? state, out eStructs? buildingType, out PlayerRelationship? relationship, out int? povPlayerId);

        List<int> result = new List<int>();
        GameBuildingManagerAPI.Instance.ExecuteQuery(result, GameBuildingManagerAPI.BuildingPredicates.IsWithinSphere(x, y, radius), state, buildingType, relationship, povPlayerId);

        return [.. result];
    }

    /// <summary>
    /// Finds all building IDs on the map, with optional filters.
    /// </summary>
    /// <param name="options">
    /// Optional. A Lua table containing query parameters:
    /// <list type="bullet">
    /// <item><description><b>state</b>: Filters for buildings in a specific <see cref="AliveState"/> (e.g., `eAliveState.IsAlive`).</description></item>
    /// <item><description><b>buildingType</b>: Filters for a specific type of building using the <see cref="eStructs"/> enum.</description></item>
    /// <item><description><b>relationship</b>: Filters buildings based on their allegiance to a player, using <see cref="PlayerRelationship"/>.</description></item>
    /// <item><description><b>povPlayer</b>: The player ID to use for the 'relationship' check.</description></item>
    /// </list>
    /// </param>
    /// <returns>An array of one-based building IDs for all buildings that match the criteria. Each value
    /// can be passed directly to any <c>Building_*</c> function that takes a buildingId.</returns>
    /// <example>
    /// The following Lua code finds all active Granaries owned by Player 1.
    /// <code>
    /// local granaries = Building_Query({
    ///     state = eAliveState.IsAlive,
    ///     buildingType = eStructs.STRUCT_GRANARY,
    ///     relationship = ePlayerRelationship.Allied,
    ///     povPlayer = 1
    /// })
    /// 
    /// print("Player 1 has " .. #granaries .. " granaries.")
    /// </code>
    /// </example>
    [LuaApiExport("Query")]
    public static int[] GetAllBuildingIds(LuaTable options = null)
    {
        ParseOptions(options, out AliveState? state, out eStructs? buildingType, out PlayerRelationship? relationship, out int? povPlayerId);

        List<int> result = new List<int>();
        GameBuildingManagerAPI.Instance.GetAllBuildings(result, state, buildingType, relationship, povPlayerId);
        return [.. result];
    }

    private static void ParseOptions(LuaTable options, out AliveState? state, out eStructs? buildingType, out PlayerRelationship? relationship, out int? povPlayerId)
    {
        // Default values
        state = null;
        buildingType = null;
        relationship = PlayerRelationship.Any;
        povPlayerId = 0;

        if (options == null)
        {
            LogHelper.Warning($"Options table is null!");
            return;
        }

        // Parse state filter (e.g., state = 'IsAlive')
        if (options["state"] is AliveState parsedState)
        {
            state = parsedState;
        }

        if (options["buildingType"] is eStructs parsedBuildingType)
        {
            buildingType = parsedBuildingType;
        }

        // Parse relationship filter (e.g., relationship = 'Enemy', povPlayer = 1)
        if (options["relationship"] is PlayerRelationship parsedRel)
        {
            relationship = parsedRel;
            if (options["povPlayer"] != null)
            {
                povPlayerId = Convert.ToInt32(options["povPlayer"]);
            }
        }
    }
}
