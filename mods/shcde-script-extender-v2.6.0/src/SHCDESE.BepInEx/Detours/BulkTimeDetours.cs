using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using Serilog;
using SHCDESE.API;
using SHCDESE.API.Components.Timer;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.Logging;
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace SHCDESE.Detours;

/// <summary>
/// Manages the native detours (hooks) for the game's time and date-related functions.
/// </summary>
[SuppressUnmanagedCodeSecurity]
internal class BulkTimeDetours
{
    private HookTransaction? tx;

    /// <summary>
    /// Scans game memory for the target function signatures and applies the detours.
    /// </summary>
    /// <param name="memory">A span representing the game's process memory to be scanned.</param>
    public BulkTimeDetours(ReadOnlySpan<byte> memory, ScanRegion region)
    {
        LogHelper.Information($"Applying");

        UInt64 currentImageBase = (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle;
        tx ??= new HookTransaction(region, Plugin.Instance.LoggerFactory);

        tx.AddDetour(c_game_update_datetime_hook,
            "FF 81 ?? ?? ?? ?? 45 33 D2",
            c_game_update_datetime_hook_impl);

        // This hook serves as the main entry point for all our deterministic, pre-tick logic.
        // It is placed inside the native game's main update loop, just before it processes in-game date/time.
        // This ensures our systems (timers, triggers) run in a valid and synchronized context.
        // For reference, this position is a bit above the c_game_update_datetime call
        tx.AddContextHook(c_game_handle_time_stuff,
             "FF 87 ? ? ? ? 33 DB",
             static ctx =>
             {
                try
                {
                    DeterministicClock.Tick();
                    bool isPaused = GamePlayerManagerAPI.Instance.IsLocalPaused();

                    // get the canon game tick
                    int currentTick = Director.instance.getSimTickCount();

                    // Trigger System Update
                    if (!isPaused)
                        GameTriggerManager.Instance.Update();

                    // R3 FrameProvider
                    FrameState frameState = new FrameState(currentTick, DeterministicClock.TotalGameTimeUnits, isPaused);
                    GameTimeManagerAPI.Instance.GetFrameProvider().Tick(in frameState);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "c_game_update_datetime_callsite caught an exception within ticker execution");
                }
            }, new RedBird.X64.Hooks.Context.ContextHookOptions() {  Registers = RedBird.X64.Assembly.X64SmartCPUContextRegs.Volatile });

        tx.Commit();
    }

    internal static HookHandle<X64InlineHook> c_game_handle_time_stuff = new();

    // __int64 __fastcall c_game_update_datetime(__int64 pPlayerManager, int a2, int counter)
    // FF 81 ?? ?? ?? ?? 45 33 D2
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_update_datetime_delegate(IntPtr pPlayerManager, int a2, int counter);
    internal static DetourHandle<c_game_update_datetime_delegate> c_game_update_datetime_hook = new();
    /// <summary>
    /// The managed callback that replaces the original game function.This implementation is our hook's entry point.
    /// </summary>
    /// <remarks>
    /// This method is executed every time the game's original `c_game_update_datetime` function is called.
    /// Its primary role is to notify our <see cref="GameTimeManagerAPI"/> that a date change has occurred.
    /// After notifying our API, it calls the original game function via the trampoline to ensure normal game execution continues.
    /// </remarks>
    /// <returns>The result of the original game function.</returns>
    public static Int64 c_game_update_datetime_hook_impl(IntPtr pPlayerManager, int a2, int counter)
    {
        //LogHelper.Debug($"pGameUnitManager={pPlayerManager.ToString("X16")}, a2={a2}, counter={counter}");
        GameTimeManagerAPI.Instance.UpdateDateTime();

        return c_game_update_datetime_hook.Original(pPlayerManager, a2, counter);
    }
}
