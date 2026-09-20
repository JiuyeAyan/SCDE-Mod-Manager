using Serilog;
using Serilog.Events;
using SHCDESE.API;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Numerics;

namespace SHCDESE.Lua;

/// <summary>
/// Provides some misc functions to Lua.
/// </summary>
public static class LuaMisc
{
    /// <summary>
    /// Registers the misc functions into the provided Lua state.
    /// </summary>
    /// <param name="lua">The <see cref="NLua.Lua"/> instance that the functions will be registered into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedStaticMethods(typeof(LuaMisc));
    }


    /// <summary>
    /// Sets the log level for the game.
    /// </summary>
    [LuaApiExport("SetLogLevel")]
    public static void SetLogLevel(LogEventLevel level)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            Plugin.Instance.LogLevelSwitch.MinimumLevel = level;
        });
    }

    /// <summary>
    /// Logs to serilog
    /// </summary>
    /// <param name="level">The log level to use.</param>
    /// <param name="luaText">The text for this logging entry.</param>
    [LuaApiExport("Log")]
    public static void LuaLog(LogEventLevel level, string luaText)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            string text = $"[Lua]: {luaText}";
            switch (level)
            {
                case LogEventLevel.Verbose:
                    Log.Verbose(text);
                    break;
                case LogEventLevel.Debug:
                    Log.Debug(text);
                    break;
                case LogEventLevel.Information:
                    Log.Information(text);
                    break;
                case LogEventLevel.Warning:
                    Log.Warning(text);
                    break;
                case LogEventLevel.Error:
                    Log.Error(text);
                    break;
                case LogEventLevel.Fatal:
                    Log.Fatal(text);
                    break;
            }
        });
    }

    /// <summary>
    /// Returns the current game version.
    /// </summary>
    /// <param name="onCompleteCallback">The result callback</param>
    [LuaApiExport("GetGameVersionAsync")]
    public static void GetGameVersion(NLua.LuaFunction onCompleteCallback)
    {
        UnityMainThreadDispatcher.DispatchGetAsync(MiscAPI.Instance.GetGameVersion, onCompleteCallback);
    }

    /// <summary>
    /// Vector2 of UInt16's
    /// </summary>
    /// <param name="x">X Param</param>
    /// <param name="y">Y Param</param>
    /// <returns>The Vector2 (UInt16)</returns>
    [LuaApiExport("Vec2UShort")]
    public static UnmanagedVector2<UInt16> Vec2UShort(UInt16 x, UInt16 y)
    {
        return new UnmanagedVector2<UInt16> { X = x, Y = y };
    }

    /// <summary>
    /// Vector2 of Int16's
    /// </summary>
    /// <param name="x">X Param</param>
    /// <param name="y">Y Param</param>
    /// <returns>The Vector2 (Int16)</returns>
    [LuaApiExport("Vec2Short")]
    public static UnmanagedVector2<Int16> Vec2Short(Int16 x, Int16 y)
    {
        return new UnmanagedVector2<Int16> { X = x, Y = y };
    }

    /// <summary>
    /// Vector2 of int's
    /// </summary>
    /// <param name="x">X Param</param>
    /// <param name="y">Y Param</param>
    /// <returns>The Vector2 (Int)</returns>
    [LuaApiExport("Vec2Int")]
    public static UnmanagedVector2<int> Vec2Int(int x, int y)
    {
        return new UnmanagedVector2<int> { X = x, Y = y };
    }

    /// <summary>
    /// Vector2 of uint's
    /// </summary>
    /// <param name="x">X Param</param>
    /// <param name="y">Y Param</param>
    /// <returns>The Vector2 (UInt)</returns>
    [LuaApiExport("Vec2UInt")]
    public static UnmanagedVector2<uint> Vec2UInt(uint x, uint y)
    {
        return new UnmanagedVector2<uint> { X = x, Y = y };
    }

    /// <summary>
    /// Default Vector3 of floats
    /// </summary>
    /// <param name="x">X Param</param>
    /// <param name="y">Y Param</param>
    /// <param name="z">Z Param</param>
    /// <returns>The Vector3</returns>
    [LuaApiExport("Vec3")]
    public static Vector3 Vec3(float x, float y, float z)
    {
        return new Vector3 { X = x, Z = y, Y = z };

    }

    /// <summary>
    /// Default Vector2 of floats
    /// </summary>
    /// <param name="x">X Param</param>
    /// <param name="y">Y Param</param>
    /// <returns>The Vector2</returns>
    [LuaApiExport("Vec2")]
    public static Vector2 Vec2(float x, float y)
    {
        return new Vector2 { X = x, Y = y };
    }

    /// <summary>
    /// A UnityEngine Color
    /// </summary>
    /// <param name="r">Red</param>
    /// <param name="g">Green</param>
    /// <param name="b">Blue</param>
    /// <param name="a">Alpha</param>
    /// <returns>The Color</returns>
    [LuaApiExport("Color")]
    public static UnityEngine.Color Color(float r, float g, float b, float a)
    {
        return new UnityEngine.Color { r = r, g = g, b = b, a = a };
    }

}