using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.Logging;
using System;
using System.Security;
using static Iced.Intel.AssemblerRegisters;

namespace SHCDESE.Detours;

[SuppressUnmanagedCodeSecurity]
public class BulkVEHDetours
{
    public BulkVEHDetours(ReadOnlySpan<byte> memory, ScanRegion region, HookTransaction tx, DataScanner scanner)
    {
        LogHelper.Information($"Applying");
        UInt64 currentImageBase = (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle;

        // The game has a custom VEH installed just for access violation exceptions for a fail-fast exit.
        // To properly produce crash dumps, this hook disables it entirely and lets the SE VEH and unity parse the exception.
        if (Plugin.Instance.EnableNativeCrashHandler.Value)
        {
            tx.AddInline(c_game_veh_handler_for_access_violations,
                "48 83 EC ? 48 8B 01 81 38", static (asm, overwritten, returnAddress) =>
                {
                    asm.xor(eax, eax);
                    asm.ret();
                });
        }

    }

    internal static void RestoreOriginalHandler()
    {
        c_game_veh_handler_for_access_violations.Hook?.Disable();
    }

    //
    // __int64 __fastcall c_game_veh_handler_for_access_violations(_DWORD **a1)
    // 48 83 EC ? 48 8B 01 81 38
    //
    internal static HookHandle<X64InlineHook> c_game_veh_handler_for_access_violations = new();
}
