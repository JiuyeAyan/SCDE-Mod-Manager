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
/// Exposes the GameUnitManagerAPI functionality to the Lua scripting environment.
/// </summary>
[LuaApiNamespace("Unit")]
public static unsafe class LuaUnitAPI
{
    /// <summary>
    /// Registers all unit-related functions and helper methods with the specified Lua instance.
    /// </summary>
    /// <param name="lua">The Lua state to register the functions into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        
        // Register all public INSTANCE methods from the GameUnitManagerAPI singleton
        // that have the [LuaApiExport] attribute.
        lua.RegisterExportedMethods(GameUnitManagerAPI.Instance);
        lua.RegisterExportedMethods(GameAfterImageManager.Instance);

        // Register all public STATIC methods from this LuaUnitAPI class
        // that have the [LuaApiExport] attribute.
        lua.RegisterExportedStaticMethods(typeof(LuaUnitAPI));

        // --- Metatable Helpers ---
        lua.DoString(@"function Unit_CreateInterface(id)
                return setmetatable({id = id}, {
                    __index = function(t, k)
                        return Unit_GetField(t.id, k)
                    end,
                    __newindex = function(t, k, v)
                        Unit_SetField(t.id, k, v)
                    end
                })
            end", "lua_unitapi");
    }

    /// <summary>
    /// Sets the unit good costs for the specified chimp unit.
    /// </summary>
    /// <param name="chimp">The chimp unit for which to set the good costs.</param>
    /// <param name="cost1">The value of the first good cost to assign to the unit.</param>
    /// <param name="cost2">The value of the second good cost to assign to the unit.</param>
    /// <param name="cost3">The value of the third good cost to assign to the unit.</param>
    /// <param name="cost4">The value of the fourth good cost to assign to the unit.</param>
    [LuaApiExport("SetGoodCosts")]
    public static void SetUnitGoodCost(eChimps chimp, eGoods cost1, eGoods cost2, eGoods cost3, eGoods cost4)
    {
        GameUnitManagerAPI.Instance.SetUnitGoodCosts(chimp, new UnitGoodCosts(cost1.To32(), cost2.To32(), cost3.To32(), cost4.To32()));
    }

    /// <summary>
    /// Pops the default (maximum) health for a specific unit type.
    /// </summary>
    /// <param name="chimp">The unit type.</param>
    /// <returns>The default health value.</returns>
    [LuaApiExport("PopDefaultHealth")]
    public static UInt32 PopDefaultHealth(eChimps chimp) => GameUnitManagerAPI.Instance._healthDefaultsArray.Pop((int)chimp);
    
    /// <summary>
    /// Pushes the default (maximum) health for a specific unit type.
    /// </summary>
    /// <param name="chimp">The unit type.</param>
    /// <param name="value">The new default health value to set.</param>
    [LuaApiExport("PushDefaultHealth")]
    public static void PushDefaultHealth(eChimps chimp, UInt32 value) => GameUnitManagerAPI.Instance._healthDefaultsArray.Push((int)chimp, value);

    /// <summary>
    /// Pops the default speed for a specific unit type.
    /// </summary>
    /// <param name="chimp">The unit type.</param>
    /// <returns>The default speed value. A lower value means faster movement.</returns>
    [LuaApiExport("PopDefaultSpeed")]
    public static UInt16 PopDefaultSpeed(eChimps chimp)
    {
        return (UInt16)(GameUnitManagerAPI.Instance._speedDefaultsArray.Pop((int)chimp) & 0xFFFF);
    }

    /// <summary>
    /// Pushes the default speed for a specific unit type.
    /// </summary>
    /// <param name="chimp">The unit type.</param>
    /// <param name="value">The new default speed value to set (clamped between 0 and 6).</param>
    [LuaApiExport("PushDefaultSpeed")]
    public static void PushDefaultSpeed(eChimps chimp, UInt16 value)
    {
        if (value > 6)
            value = 6;

        UInt32 current = GameUnitManagerAPI.Instance._speedDefaultsArray.Pop((int)chimp);
        UInt32 newValue = (current & 0xFFFF0000u) | value;
        GameUnitManagerAPI.Instance._speedDefaultsArray.Push((int)chimp, newValue);
    }

    /// <summary>
    /// Pops the unit fire damage.
    /// 100 = Default for almost all units.
    /// </summary>
    /// <param name="unit">The unit type to query for</param>
    /// <returns>Fire damage to unit</returns>
    [LuaApiExport("PopFireDamage")]
    public static int PopFireDamage(eChimps unit) => GameUnitManagerAPI.Instance._unitFireDamageDict.Pop(unit);

    /// <summary>
    /// Pushes the unit fire damage.
    /// 100 = Default for almost all units.
    /// </summary>
    /// <param name="unit">The unit type to query for</param>
    /// <param name="damage">The new received fire damage</param>
    [LuaApiExport("PushFireDamage")]
    public static void PushFireDamage(eChimps unit, int damage) => GameUnitManagerAPI.Instance._unitFireDamageDict.Push(unit, damage);

    /// <summary>
    /// Pops the unit heal by a bedouin healer
    /// 10 = Default for all units.
    /// </summary>
    /// <param name="unit">The unit type to query for</param>
    /// <returns>Bedouin heal to unit</returns>
    [LuaApiExport("PopBedouinHeal")]
    public static int PopBedouinHeal(eChimps unit) => GameUnitManagerAPI.Instance._unitBedouinHealingDict.Pop(unit);

    /// <summary>
    /// Pushes the unit heal by a bedouin healer.
    /// 10 = Default for all units.
    /// </summary>
    /// <param name="unit">The unit type to query for</param>
    /// <param name="heal">The new heal amount</param>
    [LuaApiExport("PushBedouinHeal")]
    public static void PushBedouinHeal(eChimps unit, int heal) => GameUnitManagerAPI.Instance._unitBedouinHealingDict.Push(unit, heal);

    /// <summary>
    /// Gets the base damage a melee unit inflicts upon a target unit type.
    /// </summary>
    /// <param name="source">The attacking melee unit type (<see cref="eChimps"/>).</param>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <returns>The base damage value from the melee damage table.</returns>
    [LuaApiExport("PopMeleeDamageFromTo")]
    public static int PopMeleeDamageFromTo(eChimps source, eChimps target) => GameUnitManagerAPI.Instance.MeleeDamageLookupTable.Pop((int)source, (int)target);

    /// <summary>
    /// Sets the base damage a melee unit inflicts upon a target unit type.
    /// </summary>
    /// <param name="source">The attacking melee unit type (<see cref="eChimps"/>).</param>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <param name="value">The new base damage value to set in the melee damage table.</param>
    [LuaApiExport("PushMeleeDamageFromTo")]
    public static void PushMeleeDamageFromTo(eChimps source, eChimps target, int value) => GameUnitManagerAPI.Instance.MeleeDamageLookupTable.Push((int)source, (int)target, value);

    /// <summary>
    /// Gets the base damage from a Eunuch's area-of-effect attack upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <returns>The base damage value from the Eunuch AOE damage table.</returns>
    [LuaApiExport("PopMeleeEunuchAOEDamageTo")]
    public static int PopMeleeEunuchAOEDamageTo(eChimps target) => GameUnitManagerAPI.Instance._meleeEunuchAOEDamageArray.Pop((int)target);

    /// <summary>
    /// Sets the base damage from a Eunuch's area-of-effect attack upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <param name="value">The new base damage value to set in the Eunuch AOE damage table.</param>
    [LuaApiExport("PushMeleeEunuchAOEDamageTo")]
    public static void PushMeleeEunuchAOEDamageTo(eChimps target, int value) => GameUnitManagerAPI.Instance._meleeEunuchAOEDamageArray.Push((int)target, value);

    /// <summary>
    /// Gets the base damage from an arrow projectile upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <returns>The base damage value from the arrow damage table.</returns>
    [LuaApiExport("PopRangedArrowDamageTo")]
    public static int PopRangedArrowDamageTo(eChimps target) => GameUnitManagerAPI.Instance._rangedArrowDamageArray.Pop((int)target);

    /// <summary>
    /// Sets the base damage from an arrow projectile upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <param name="value">The new base damage value to set in the arrow damage table.</param>
    [LuaApiExport("PushRangedArrowDamageTo")]
    public static void PushRangedArrowDamageTo(eChimps target, int value) => GameUnitManagerAPI.Instance._rangedArrowDamageArray.Push((int)target, value);

    /// <summary>
    /// Gets the base damage from a bolt projectile (e.g., from a Crossbowman or Ballista) upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <returns>The base damage value from the bolt damage table.</returns>
    [LuaApiExport("Unit_PopRangedBoltDamageTo")]
    public static int PopRangedBoltDamageTo(eChimps target) => GameUnitManagerAPI.Instance._rangedBoltDamageArray.Pop((int)target);

    /// <summary>
    /// Sets the base damage from a bolt projectile upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <param name="value">The new base damage value to set in the bolt damage table.</param>
    [LuaApiExport("PushRangedBoltDamageTo")]
    public static void PushRangedBoltDamageTo(eChimps target, int value) => GameUnitManagerAPI.Instance._rangedBoltDamageArray.Push((int)target, value);

    /// <summary>
    /// Gets the base damage from a slinger's stone projectile upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <returns>The base damage value from the slinger damage table.</returns>
    [LuaApiExport("PopRangedSlingerDamageTo")]
    public static int PopRangedSlingerDamageTo(eChimps target) => GameUnitManagerAPI.Instance._rangedSlingerDamageArray.Pop((int)target);

    /// <summary>
    /// Sets the base damage from a slinger's stone projectile upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <param name="value">The new base damage value to set in the slinger damage table.</param>
    [LuaApiExport("PushRangedSlingerDamageTo")]
    public static void PushRangedSlingerDamageTo(eChimps target, int value) => GameUnitManagerAPI.Instance._rangedSlingerDamageArray.Push((int)target, value);

    /// <summary>
    /// Gets the base damage from a javelin projectile upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <returns>The base damage value from the javelin damage table.</returns>
    [LuaApiExport("PopRangedJavelinDamageTo")]
    public static int PopRangedJavelinDamageTo(eChimps target) => GameUnitManagerAPI.Instance._rangedJavelinDamageArray.Pop((int)target);

    /// <summary>
    /// Sets the base damage from a javelin projectile upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <param name="value">The new base damage value to set in the javelin damage table.</param>
    [LuaApiExport("PushRangedJavelinDamageTo")]
    public static void PushRangedJavelinDamageTo(eChimps target, int value) => GameUnitManagerAPI.Instance._rangedJavelinDamageArray.Push((int)target, value);

    /// <summary>
    /// Gets the value of a field from a unit's data structure. Exposed to Lua for metatable use.
    /// </summary>
    /// <param name="id">The ID of the unit.</param>
    /// <param name="field">The name of the field to retrieve.</param>
    /// <returns>The value of the field, or null if the unit or field is not found or not exposed.</returns>
    [LuaApiExport("GetField")]
    public static object GetUnitField(int id, string field)
    {
        if (!GameUnitManagerAPI.Instance.TryGetUnitById(id, out GameUnit* ptr))
        {
            LogHelper.Warning($"Could not find unit by id: {id}");
            return null;
        }
        FieldInfo info = typeof(GameUnit).GetField(field, BindingFlags.Public | BindingFlags.Instance);
        if (info == null || !Attribute.IsDefined(info, typeof(LuaExposedAttribute)))
        {
            LogHelper.Warning($"Could not find field: {field} (or not exposed)");
            return null;
        }

        TypedReference tr = __makeref(*ptr);
        return info.GetValueDirect(tr);
    }

    /// <summary>
    /// Sets the value of a field in a unit's data structure. Exposed to Lua for metatable use.
    /// </summary>
    /// <param name="id">The ID of the unit.</param>
    /// <param name="field">The name of the field to modify.</param>
    /// <param name="value">The new value to set.</param>
    [LuaApiExport("SetField")]
    public static void SetUnitField(int id, string field, object value)
    {
        if (!GameUnitManagerAPI.Instance.TryGetUnitById(id, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {id}");
            return;
        }
        StructFieldSetter.SetField(unit, field, value, typeof(LuaExposedAttribute));
    }

    /// <summary>
    /// Finds all unit IDs within a rectangular area, with optional filters.
    /// </summary>
    /// <param name="options">A Lua table containing query parameters: x, y, width, height, and optional 'state', 'relationship', and 'povPlayer'.</param>
    /// <returns>An array of one-based unit IDs that match the criteria. Each value can be passed
    /// directly to any <c>Unit_*</c> function that takes a unitId.</returns>
    /// <example>
    /// <code>
    /// local enemies = Unit_FindInRect({ x=100, y=100, width=50, height=50, relationship=ePlayerRelationship.Enemy, povPlayer=1 })
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
        ParseOptions(options, out AliveState? state, out eChimps? unitType, out PlayerRelationship? relationship, out int? povPlayerId);

        List<int> result = new List<int>();
        GameUnitManagerAPI.Instance.ExecuteQuery(result, GameUnitManagerAPI.UnitPredicates.IsWithinRect(x, y, width, height), state, unitType, relationship, povPlayerId);
        return [.. result];
    }

    /// <summary>
    /// Finds all unit IDs within a spherical area, with optional filters.
    /// </summary>
    /// <param name="options">A Lua table containing query parameters: x, y, radius, and optional 'state', 'relationship', and 'povPlayer'.</param>
    /// <returns>An array of one-based unit IDs that match the criteria. Each value can be passed
    /// directly to any <c>Unit_*</c> function that takes a unitId.</returns>
    /// <example>
    /// <code>
    /// local allies = Unit_FindInSphere({ x=100, y=100, radius=20, relationship=ePlayerRelationship.Allied, povPlayer=1 })
    /// </code>
    /// </example>
    [LuaApiExport("FindInSphere")]
    public static int[] GetUnitsInSphere(LuaTable options)
    {
        // Required spatial parameters
        int x = Convert.ToInt32(options["x"]);
        int y = Convert.ToInt32(options["y"]);
        int radius = Convert.ToInt32(options["radius"]);

        // Optional filter parameters
        ParseOptions(options, out AliveState? state, out eChimps? unitType, out PlayerRelationship? relationship, out int? povPlayerId);

        List<int> result = new List<int>();
        GameUnitManagerAPI.Instance.ExecuteQuery(result, GameUnitManagerAPI.UnitPredicates.IsWithinSphere(x, y, radius), state, unitType, relationship, povPlayerId);

        return [.. result];
    }

    /// <summary>
    /// Finds all unit IDs on the map, with optional filters.
    /// </summary>
    /// <param name="options">Optional. A Lua table containing query parameters: 'state', 'relationship', and 'povPlayer'.</param>
    /// <returns>An array of one-based unit IDs that match the criteria. Each value can be passed
    /// directly to any <c>Unit_*</c> function that takes a unitId.</returns>
    /// <example>
    /// <code>
    /// local alliedUnits = Unit_GetAllUnitIds({
    /// state = eAliveState.IsAlive,
    /// relationship = ePlayerRelationship.Allied,
    /// povPlayer = 1
    /// })
    /// print("units: "..alliedUnits.Length)
    /// </code>
    /// </example>
    [LuaApiExport("Unit_GetAllUnitIds")]
    public static int[] GetAllUnitIds(LuaTable options = null)
    {
        ParseOptions(options, out AliveState? state, out eChimps? unitType, out PlayerRelationship? relationship, out int? povPlayerId);

        List<int> result = new List<int>();
        GameUnitManagerAPI.Instance.GetAllUnits(result, state, unitType, relationship, povPlayerId);
        return [.. result];
    }

    private static void ParseOptions(LuaTable options, out AliveState? state, out eChimps? unitType, out PlayerRelationship? relationship, out int? povPlayerId)
    {
        // Default values
        state = null;
        unitType = null;
        relationship = PlayerRelationship.Any;
        povPlayerId = 1;

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

        if (options["unitType"] is eChimps parsedUnitType)
        {
            unitType = parsedUnitType;
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
