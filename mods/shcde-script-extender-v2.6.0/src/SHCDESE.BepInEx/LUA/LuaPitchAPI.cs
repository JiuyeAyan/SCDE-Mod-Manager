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
/// Exposes the GamePitchManagerAPI functionality to the Lua scripting environment.
/// </summary>
[LuaApiNamespace("Pitch")]
public unsafe static class LuaPitchAPI
{
    /// <summary>
    /// Registers all pitch-related functions with the specified Lua instance.
    /// </summary>
    /// <param name="lua">The Lua state to register the functions into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedMethods(GamePitchManagerAPI.Instance);
        lua.RegisterExportedStaticMethods(typeof(LuaPitchAPI));

        lua.DoString(@"function Pitch_CreateInterface(id)
                return setmetatable({id = id}, {
                    __index = function(t, k)
                        return Pitch_GetField(t.id, k)
                    end,
                    __newindex = function(t, k, v)
                        Pitch_SetField(t.id, k, v)
                    end
                })
            end", "lua_pitchapi");
    }

    // -------------------------------------------------------------------------
    // Field access
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets the value of a field from a pitch descriptor by its slot ID.
    /// Exposed to Lua for metatable use via <c>Pitch_CreateInterface</c>.
    /// </summary>
    /// <param name="pitchId">The pitch slot ID.</param>
    /// <param name="field">The name of the field to retrieve (must have <see cref="LuaExposedAttribute"/>).</param>
    /// <returns>The field value, or <c>null</c> if the ID or field is invalid/not exposed.</returns>
    /// <example>
    /// <code>
    /// local p = Pitch_CreateInterface(id)
    /// print(p.TileX, p.TileY, p.OwnerId)
    /// </code>
    /// </example>
    [LuaApiExport("GetField")]
    public static object GetPitchField(int pitchId, string field)
    {
        if (!GamePitchManagerAPI.Instance.TryGetPitchById(pitchId, out GamePitchDescriptor* ptr))
        {
            LogHelper.Warning($"Pitch_GetField: could not find pitch id={pitchId}");
            return null;
        }

        FieldInfo info = typeof(GamePitchDescriptor).GetField(field, BindingFlags.Public | BindingFlags.Instance);
        if (info == null || !Attribute.IsDefined(info, typeof(LuaExposedAttribute)))
        {
            LogHelper.Warning($"Pitch_GetField: field '{field}' not found or not exposed");
            return null;
        }

        TypedReference tr = __makeref(*ptr);
        return info.GetValueDirect(tr);
    }

    /// <summary>
    /// Sets the value of a field in a pitch descriptor by its slot ID.
    /// Exposed to Lua for metatable use via <c>Pitch_CreateInterface</c>.
    /// </summary>
    /// <param name="pitchId">The pitch slot ID.</param>
    /// <param name="field">The name of the field to modify (must have <see cref="LuaExposedAttribute"/>).</param>
    /// <param name="value">The new value to set.</param>
    /// <example>
    /// <code>
    /// local p = Pitch_CreateInterface(id)
    /// p.OwnerId = 2
    /// </code>
    /// </example>
    [LuaApiExport("SetField")]
    public static void SetPitchField(int pitchId, string field, object value)
    {
        if (!GamePitchManagerAPI.Instance.TryGetPitchById(pitchId, out GamePitchDescriptor* ptr))
        {
            LogHelper.Warning($"Pitch_SetField: could not find pitch id={pitchId}");
            return;
        }

        StructFieldSetter.SetField(ptr, field, value, typeof(LuaExposedAttribute));
    }

    // -------------------------------------------------------------------------
    // Spatial query functions
    // -------------------------------------------------------------------------

    /// <summary>
    /// Finds all pitch slot IDs within a rectangular area, with optional filters.
    /// </summary>
    /// <param name="options">
    /// A Lua table with keys: <c>x</c>, <c>y</c>, <c>width</c>, <c>height</c> (required),
    /// and optionally <c>owner</c>, <c>relationship</c> (<see cref="PlayerRelationship"/>), <c>povPlayer</c>.
    /// </param>
    /// <returns>An array of one-based pitch IDs that match the criteria. Each value can be passed
    /// directly to any <c>Pitch_*</c> function that takes a pitchId.</returns>
    /// <example>
    /// <code>
    /// -- All enemy pitch in area
    /// local ids = Pitch_FindInRect({ x=100, y=100, width=50, height=50, relationship=ePlayerRelationship.Enemy, povPlayer=1 })
    /// for i = 1, ids.Length do
    ///     print("Enemy pitch id: " .. ids[i-1])
    /// end
    /// </code>
    /// </example>
    [LuaApiExport("FindInRect")]
    public static int[] GetPitchInRect(LuaTable options)
    {
        int x = Convert.ToInt32(options["x"]);
        int y = Convert.ToInt32(options["y"]);
        int width = Convert.ToInt32(options["width"]);
        int height = Convert.ToInt32(options["height"]);

        ParseOptions(options, out int? ownerFilter, out PlayerRelationship? relationship, out int? povPlayerId);

        List<int> result = new List<int>();
        GamePitchManagerAPI.Instance.GetPitchWithinRect(result, x, y, width, height, ownerFilter, relationship, povPlayerId);
        return [.. result];
    }

    /// <summary>
    /// Finds all pitch slot IDs within a spherical area, with optional filters.
    /// </summary>
    /// <param name="options">
    /// A Lua table with keys: <c>x</c>, <c>y</c>, <c>radius</c> (required),
    /// and optionally <c>owner</c>, <c>relationship</c> (<see cref="PlayerRelationship"/>), <c>povPlayer</c>.
    /// </param>
    /// <returns>An array of one-based pitch IDs that match the criteria. Each value can be passed
    /// directly to any <c>Pitch_*</c> function that takes a pitchId.</returns>
    /// <example>
    /// <code>
    /// -- All allied pitch within 20 tiles of a point
    /// local ids = Pitch_FindInSphere({ x=400, y=400, radius=20, relationship=ePlayerRelationship.Allied, povPlayer=1 })
    /// print("Nearby allied pitch count: " .. ids.Length)
    /// </code>
    /// </example>
    [LuaApiExport("FindInSphere")]
    public static int[] GetPitchInSphere(LuaTable options)
    {
        int x = Convert.ToInt32(options["x"]);
        int y = Convert.ToInt32(options["y"]);
        int radius = Convert.ToInt32(options["radius"]);

        ParseOptions(options, out int? ownerFilter, out PlayerRelationship? relationship, out int? povPlayerId);

        List<int> result = new List<int>();
        GamePitchManagerAPI.Instance.GetPitchWithinSphere(result, x, y, radius, ownerFilter, relationship, povPlayerId);
        return [.. result];
    }

    /// <summary>
    /// Returns all pitch slot IDs on the map, with optional filters.
    /// </summary>
    /// <param name="options">
    /// Optional Lua table with keys: <c>owner</c>, <c>relationship</c> (<see cref="PlayerRelationship"/>), <c>povPlayer</c>.
    /// </param>
    /// <returns>An array of one-based pitch IDs that match the criteria. Each value can be passed
    /// directly to any <c>Pitch_*</c> function that takes a pitchId.</returns>
    /// <example>
    /// <code>
    /// -- All pitch owned by player 2
    /// local ids = Pitch_GetAllIds({ owner=2 })
    /// print("Player 2 pitch count: " .. ids.Length)
    ///
    /// -- All pitch on map
    /// local allIds = Pitch_GetAllIds()
    /// </code>
    /// </example>
    [LuaApiExport("GetAllIds")]
    public static int[] GetAllPitchIds(LuaTable options = null)
    {
        ParseOptions(options, out int? ownerFilter, out PlayerRelationship? relationship, out int? povPlayerId);

        List<int> result = new List<int>();
        GamePitchManagerAPI.Instance.GetAllPitch(result, ownerFilter, relationship, povPlayerId);
        return [.. result];
    }

    // -------------------------------------------------------------------------
    // Options parser
    // -------------------------------------------------------------------------

    private static void ParseOptions(
        LuaTable options,
        out int? ownerFilter,
        out PlayerRelationship? relationship,
        out int? povPlayerId)
    {
        ownerFilter = null;
        relationship = PlayerRelationship.Any;
        povPlayerId = 1;

        if (options == null)
            return;

        if (options["owner"] != null)
            ownerFilter = Convert.ToInt32(options["owner"]);

        if (options["relationship"] is PlayerRelationship parsedRel)
        {
            relationship = parsedRel;
            if (options["povPlayer"] != null)
                povPlayerId = Convert.ToInt32(options["povPlayer"]);
        }
    }
}
