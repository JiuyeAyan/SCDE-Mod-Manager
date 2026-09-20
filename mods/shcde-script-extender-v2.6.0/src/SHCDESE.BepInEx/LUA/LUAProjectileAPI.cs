using NLua;
using SHCDESE.API;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Logging;
using SHCDESE.Lua.CodeGen;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Reflection;

namespace SHCDESE.Lua;

/// <summary>
/// Exposes the GameProjectileManagerAPI functionality to the Lua scripting environment.
/// </summary>
[LuaApiNamespace("Projectile")]
public unsafe static class LuaProjectileAPI
{
    /// <summary>
    /// Registers all projectile-related functions and helper methods with the specified Lua instance.
    /// </summary>
    /// <param name="lua">The Lua state to register the functions into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedMethods(GameProjectileManagerAPI.Instance);
        lua.RegisterExportedStaticMethods(typeof(LuaProjectileAPI));

        // --- Metatable Helpers ---
        lua.DoString(@"function Projectile_CreateInterface(id)
                    return setmetatable({id = id}, {
                        __index = function(t, k)
                            return Projectile_GetField(t.id, k)
                        end,
                        __newindex = function(t, k, v)
                            Projectile_SetField(t.id, k, v)
                        end
                    })
                end", "lua_projectileapi");
    }

    /// <summary>
    /// Gets the value of a field from a projectile's data structure. Exposed to Lua for metatable use.
    /// </summary>
    /// <param name="id">The ID of the projectile.</param>
    /// <param name="field">The name of the field to retrieve.</param>
    /// <returns>The value of the field, or null if the projectile or field is not found or not exposed.</returns>
    [LuaApiExport("GetField")]
    public static object GetProjectileField(int id, string field)
    {
        if (!GameProjectileManagerAPI.Instance.TryGetProjectileById(id, out GameProjectile* ptr))
        {
            LogHelper.Warning($"Could not find projectile by id: {id}");
            return null;
        }
        FieldInfo info = typeof(GameProjectile).GetField(field, BindingFlags.Public | BindingFlags.Instance);
        if (info == null || !Attribute.IsDefined(info, typeof(LuaExposedAttribute)))
        {
            LogHelper.Warning($"Could not find field: {field} (or not exposed)");
            return null;
        }

        // Create a TypedReference that points directly to the struct in memory.
        // __makeref is an undocumented but essential keyword for this.
        TypedReference tr = __makeref(*ptr);

        // Use GetValueDirect to read the field's value without any boxing.
        // This avoids the NLua marshaller recursion and is much faster.
        return info.GetValueDirect(tr);
    }

    /// <summary>
    /// Sets the value of a field in a projectile's data structure. Exposed to Lua for metatable use.
    /// </summary>
    /// <param name="id">The ID of the projectile.</param>
    /// <param name="field">The name of the field to modify.</param>
    /// <param name="value">The new value to set.</param>
    [LuaApiExport("SetField")]
    public static void SetProjectileField(int id, string field, object value)
    {
        if (!GameProjectileManagerAPI.Instance.TryGetProjectileById(id, out GameProjectile* ptr))
        {
            LogHelper.Warning($"Could not find projectile by id: {id}");
            return;
        }
        StructFieldSetter.SetField(ptr, field, value, typeof(LuaExposedAttribute));
    }
}
