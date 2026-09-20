using NLua;
using SHCDESE.API;
using SHCDESE.API.Components.Timer;
using SHCDESE.Extensions;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;

namespace SHCDESE.Lua;

/// <summary>
/// Exposes the deterministic TimerEngine and GameTimeManagerAPI to the Lua scripting environment.
/// </summary>
[LuaApiNamespace("Time")]
public static class LuaTimeAPI
{

    /// <summary>
    /// Registers all time-related functions with the specified Lua instance.
    /// </summary>
    /// <param name="lua">The Lua state to register the functions into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedMethods(GameTimeManagerAPI.Instance);
        lua.RegisterExportedStaticMethods(typeof(LuaTimeAPI));

    }

    /// <summary>
    /// Checks if a timer with the specified handle ID is currently active and valid.
    /// </summary>
    /// <param name="handleId">The string handle returned by a timer creation function.</param>
    /// <returns><c>true</c> if the handle corresponds to an active timer; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsValidHandle")]
    public static bool IsValidHandle(string handleId)
    {
        return GameTimeManagerAPI.Instance.GetTimerEngine().IsHandleValid(handleId);
    }

    /// <summary>
    /// Schedules a savable delayed action using a global function name.
    /// </summary>
    /// <param name="milliseconds">Delay in milliseconds.</param>
    /// <param name="callbackName">The string name of the global Lua function to execute.</param>
    /// <returns>A unique string handle for the created timer.</returns>
    [LuaApiExport("AddDelayedAction")]
    public static string AddDelayedAction(int milliseconds, string callbackName)
    {
        if (string.IsNullOrEmpty(callbackName)) 
            return string.Empty;

        Action callback = CreateLuaCallback(callbackName);
        TimerEngine engine = GameTimeManagerAPI.Instance.GetTimerEngine();

        return engine.AddDelayedAction(milliseconds, callback, callbackName);
    }

    /// <summary>
    /// Schedules a savable repeated action using a global function name.
    /// </summary>
    /// <param name="milliseconds">The interval in milliseconds between each execution.</param>
    /// <param name="callbackName">The string name of the global Lua function to execute repeatedly.</param>
    /// <returns>A unique string handle for the created timer.</returns>
    [LuaApiExport("AddRepeatedAction")]
    public static string AddRepeatedAction(int milliseconds, string callbackName)
    {
        if (string.IsNullOrEmpty(callbackName)) 
            return string.Empty;

        Action callback = CreateLuaCallback(callbackName);
        TimerEngine engine = GameTimeManagerAPI.Instance.GetTimerEngine();

        return engine.AddRepeatedAction(milliseconds, callback, callbackName);
    }

    /// <summary>
    /// Schedules a non-savable, single-execution action using an anonymous Lua function.
    /// The timer is automatically removed after execution.
    /// </summary>
    /// <param name="milliseconds">The delay in milliseconds before the function is executed.</param>
    /// <param name="function">The anonymous Lua function to execute.</param>
    /// <returns>A unique string handle for the created timer, which can be used to cancel it prematurely.</returns>
    [LuaApiExport("AddOneShot")]
    public static string AddAnonymousDelayedAction(int milliseconds, LuaFunction function)
    {
        if (function == null)
        {
            LogHelper.Warning("Time_AddOneShot was called with a nil function.");
            return string.Empty;
        }

        Action callback = () =>
        {
            try
            {
                function.Call();
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "An exception occurred executing an anonymous Lua delayed timer callback.");
            }
            finally
            {
                // Dispose the function handle after execution to release the Lua registry reference.
                function.Dispose();
            }
        };

        TimerEngine engine = GameTimeManagerAPI.Instance.GetTimerEngine();

        // Pass null for callbackName so it doesn't get saved
        return engine.AddDelayedAction(milliseconds, callback, null);
    }


    /// <summary>
    /// Schedules a non-savable, repeating action using an anonymous Lua function.
    /// </summary>
    /// <param name="milliseconds">The interval in milliseconds between each execution.</param>
    /// <param name="function">The anonymous Lua function to execute repeatedly.</param>
    /// <returns>A unique string handle for the created timer, which must be used to cancel it.</returns>
    [LuaApiExport("AddRepeatingOneShot")]
    public static string AddAnonymousRepeatedAction(int milliseconds, LuaFunction function)
    {
        if (function == null)
        {
            LogHelper.Warning("Time_AddRepeatingOneShot was called with a nil function.");
            return string.Empty;
        }

        // Create a wrapper for the Lua function that will be kept alive by the timer's subscription.
        // When the timer is removed via RemoveAction, the subscription is disposed, this action
        // becomes eligible for garbage collection, and the LuaFunction's finalizer will release its Lua reference.
        Action callback = () =>
        {
            try
            {
                function.Call();
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "An exception occurred executing an anonymous Lua repeated timer callback.");
            }
        };

        TimerEngine engine = GameTimeManagerAPI.Instance.GetTimerEngine();

        // Pass null for callbackName so it doesn't get saved
        return engine.AddRepeatedAction(milliseconds, callback, null);
    }

    // Helper to create the C# Action from a Lua function name
    private static Action CreateLuaCallback(string callbackName)
    {
        return () =>
        {
            try
            {
                LuaFunction? func =  LuaManager.Instance.Lua?[callbackName] as LuaFunction;
                if (func != null)
                {
                    func.Call();
                    func.Dispose();  // Dispose after call for non-repeating timers, though harmless for repeating.
                }
                else
                {
                    LogHelper.Error($"Timer callback error: Could not find global Lua function [{callbackName}].");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"An exception occurred executing Lua timer callback [{callbackName}].");
            }
        };
    }

    /// <summary>
    /// Removes and cancels any timer using its string handle.
    /// </summary>
    /// <param name="handleId">The string handle of the timer to remove.</param>
    [LuaApiExport("RemoveAction")]
    public static void RemoveAction(string handleId)
    {
        GameTimeManagerAPI.Instance.GetTimerEngine().RemoveAction(handleId);
    }
}