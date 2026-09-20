using NLua;
using SHCDESE.API;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Logging;
using SHCDESE.Lua.CodeGen;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;

namespace SHCDESE.Lua;

/// <summary>
/// Exposes the GamePlayerManagerAPI functionality to the Lua scripting environment.
/// </summary>
[LuaApiNamespace("Player")]
public unsafe static class LuaPlayerAPI
{
    /// <summary>
    /// Registers all player-related functions and helper methods with the specified Lua instance.
    /// </summary>
    /// <param name="lua">The Lua state to register the functions into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedMethods(GamePlayerManagerAPI.Instance);
        lua.RegisterExportedStaticMethods(typeof(LuaPlayerAPI));

        // --- Metatable Helpers ---
        lua.DoString(@"function PlayerResource_CreateInterface(id)
                    return setmetatable({id = id}, {
                        __index = function(t, k)
                            return Player_GetResourceField(t.id, k)
                        end,
                        __newindex = function(t, k, v)
                            Player_SetResourceField(t.id, k, v)
                        end
                    })
                end", "lua_playerapi");

    }

    /// <summary>
    /// Set the base trade price of a good.
    /// </summary>
    /// <param name="good">The good to query</param>
    /// <param name="buyPrice">The new buy price</param>
    /// <param name="sellPrice">The new sell price</param>
    [LuaApiExport("SetTradeBasePrice")]
    public static void SetTradeBasePrice(eGoods good, int buyPrice, int sellPrice)
    {
        GamePlayerManagerAPI.Instance.SetTradeBasePrice(good, new PackedGoodPrice(buyPrice, sellPrice));
    }

    /// <summary>
    /// Set the base default trade price of a good.
    /// </summary>
    /// <param name="good">The good to query</param>
    /// <param name="buyPrice">The new buy price</param>
    /// <param name="sellPrice">The new sell price</param>
    [LuaApiExport("SetDefaultTradeBasePrice")]
    public static void SetDefaultTradeBasePrice(eGoods good, int buyPrice, int sellPrice)
    {
        GamePlayerManagerAPI.Instance.SetDefaultTradeBasePrice(good, new PackedGoodPrice(buyPrice, sellPrice));
    }

    /// <summary>
    /// Lua version of <see cref="GamePlayerManagerAPI.GetScreenCenterTilePosition"/> 
    /// </summary>
    /// <param name="onCompleteCallback">The result callback</param>
    [LuaApiExport("GetScreenCenterTilePositionAsync")]
    public static void GetScreenCenterTilePositionAsync(LuaFunction onCompleteCallback)
    {
        UnityMainThreadDispatcher.DispatchGetAsync(GamePlayerManagerAPI.Instance.GetScreenCenterTilePosition, onCompleteCallback);
    }

    /// <summary>
    /// Lua version of <see cref="GamePlayerManagerAPI.IsMapLocked"/>
    /// </summary>
    /// <param name="onCompleteCallback">The result callback</param>
    [LuaApiExport("IsMapLockedAsync")]
    public static void IsMapLockedAsync(LuaFunction onCompleteCallback)
    {
        UnityMainThreadDispatcher.DispatchGetAsync(GamePlayerManagerAPI.Instance.IsMapLocked, onCompleteCallback);
    }

    /// <summary>
    /// Lua version of <see cref="GamePlayerManagerAPI.GetRotationCentre"/> 
    /// </summary>
    /// <param name="onCompleteCallback">The result callback</param>
    [LuaApiExport("GetRotationCentreAsync")]
    public static void GetRotationCentreAsync(LuaFunction onCompleteCallback)
    {
        UnityMainThreadDispatcher.DispatchGetAsync(GamePlayerManagerAPI.Instance.GetRotationCentre, onCompleteCallback);
    }

    /// <summary>
    /// Lua version of <see cref="GamePlayerManagerAPI.GetCurrentRotation"/> 
    /// </summary>
    /// <param name="onCompleteCallback">The result callback</param>
    [LuaApiExport("GetMapRotationAsync")]
    public static void GetCurrentRotationAsync(LuaFunction onCompleteCallback)
    {
        UnityMainThreadDispatcher.DispatchGetAsync(GamePlayerManagerAPI.Instance.GetCurrentRotation, onCompleteCallback);
    }

    /// <summary>
    /// Gets whether noesis is to receive keyboard input or not.
    /// </summary>
    /// <param name="onCompleteCallback">The result callback</param>
    [LuaApiExport("GetNoesisHasKeyboardAsync")]
    public static void GetNoesisHasKeyboardAsync(LuaFunction onCompleteCallback)
    {
        UnityMainThreadDispatcher.DispatchGetAsync(GamePlayerManagerAPI.Instance.GetNoesisHasKeyboard, onCompleteCallback);
    }

    /// <summary>
    /// Gets the local frame time for the native engine.
    /// </summary>
    /// <returns>Frame time / Tickrate of the native engine.</returns>
    [LuaApiExport("GetFrameTime")]
    public static void GetFrameTime(LuaFunction onCompleteCallback)
    {
        UnityMainThreadDispatcher.DispatchGetAsync(GamePlayerManagerAPI.Instance.GetFrameTime, onCompleteCallback);
    }

    /// <summary>
    /// Gets whether the cursor is currently over any noesis GUI
    /// </summary>
    /// <param name="onCompleteCallback">The result callback</param>
    [LuaApiExport("IsOverNoesisGUIAsync")]
    public static void IsOverNoesisGUI(LuaFunction onCompleteCallback)
    {
        UnityMainThreadDispatcher.DispatchGetAsync(GamePlayerManagerAPI.Instance.IsOverNoesisGUI, onCompleteCallback);
    }

    /// <summary>
    /// Lua version of <see cref="GamePlayerManagerAPI.GetZoom"/>
    /// </summary>
    [LuaApiExport("GetZoomAsync")]
    public static void GetZoomAsync(LuaFunction onCompleteCallback)
    {
        UnityMainThreadDispatcher.DispatchGetAsync(GamePlayerManagerAPI.Instance.GetZoom, onCompleteCallback);
    }

    /// <summary>
    /// Lua version of <see cref="GamePlayerManagerAPI.GetCameraZoomSpeed"/>
    /// </summary>
    /// <returns>The current camera move speed</returns>
    [LuaApiExport("GetCameraZoomSpeedAsync")]
    public static void GetCameraZoomSpeedAsync(LuaFunction onCompleteCallback)
    {
        UnityMainThreadDispatcher.DispatchGetAsync(GamePlayerManagerAPI.Instance.GetCameraZoomSpeed, onCompleteCallback);
    }

    /// <summary>
    /// Lua version of <see cref="GamePlayerManagerAPI.GetCameraMoveSpeed"/>
    /// </summary>
    /// <returns>The current camera move speed</returns>
    [LuaApiExport("GetCameraMoveSpeedAsync")]
    public static void GetCameraMoveSpeedAsync(LuaFunction onCompleteCallback)
    {
        UnityMainThreadDispatcher.DispatchGetAsync(GamePlayerManagerAPI.Instance.GetCameraMoveSpeed, onCompleteCallback);
    }

    /// <summary>
    /// Lua version of <see cref="GamePlayerManagerAPI.IsCameraControlsDisabled"/>
    /// </summary>
    /// <returns>The current camera move speed</returns>
    [LuaApiExport("IsCameraControlsDisabledAsync")]
    public static void IsCameraControlsDisabledAsync(LuaFunction onCompleteCallback)
    {
        UnityMainThreadDispatcher.DispatchGetAsync(GamePlayerManagerAPI.Instance.IsCameraControlsDisabled, onCompleteCallback);
    }

    /// <summary>
    /// Gets the value of a field from a playerresource's data structure. Exposed to Lua for metatable use.
    /// </summary>
    /// <param name="id">The ID of the playerresource.</param>
    /// <param name="field">The name of the field to retrieve.</param>
    /// <returns>The value of the field, or null if the unit or field is not found or not exposed.</returns>
    [LuaApiExport("GetResourceField")]
    public static object GetPlayerResourceField(int id, string field)
    {
        try
        {
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(id, out GamePlayerResources* ptr))
            {
                LogHelper.Warning($"Could not find playerresource by id: {id}");
                return null;
            }
            FieldInfo info = typeof(GamePlayerResources).GetField(field, BindingFlags.Public | BindingFlags.Instance);
            if (info == null || !Attribute.IsDefined(info, typeof(LuaExposedAttribute)))
            {
                LogHelper.Warning($"Could not find field: {field} (or not exposed)");
                return null;
            }
            TypedReference tr = __makeref(*ptr);
            return info.GetValueDirect(tr);
        } 
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception during struct access");
        }
        return null;
    }

    /// <summary>
    /// Sets the value of a field in a playerresource's data structure. Exposed to Lua for metatable use.
    /// </summary>
    /// <param name="id">The ID of the playrrresource.</param>
    /// <param name="field">The name of the field to modify.</param>
    /// <param name="value">The new value to set.</param>
    [LuaApiExport("SetResourceField")]
    public static void SetPlayerResourceField(int id, string field, object value)
    {
        try
        {
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(id, out GamePlayerResources* ptr))
            {
                LogHelper.Warning($"Could not find playerresource by id: {id}");
                return;
            }
            StructFieldSetter.SetField(ptr, field, value, typeof(LuaExposedAttribute));
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception during struct access");
        }
    }
}
