using RedBird.Core.Memory;
using RedBird.Core.Memory.Scanners;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.GameGlobals;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace SHCDESE.Detours;

[SuppressUnmanagedCodeSecurity]
public class BulkChoreDetours
{
    private HookTransaction? tx;

    public BulkChoreDetours(ReadOnlySpan<byte> memory, ScanRegion region)
    {
        LogHelper.Information($"Applying");
        UInt64 currentImageBase = (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle;

        tx ??= new HookTransaction(region, Plugin.Instance.LoggerFactory);
        DataScanner scanner = DataScanner.Create(region);

        //tx.AddDetour(ref c_game_queue_chore_hook,
        //    "48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 48 89 7C 24 ? 41 54 41 56 41 57 48 83 EC ? 0F BE F2",
        //    c_game_queue_chore_hook_impl);

        Microsoft.Extensions.Logging.ILogger detourLogger = Plugin.Instance.LoggerFactory.CreateLogger("BulkChoreDetours");

        DataScanner c_game_queue_chore_scan = scanner.Scan(CompiledPattern.Parse("48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 48 89 7C 24 ? 41 54 41 56 41 57 48 83 EC ? 0F BE F2"));
        if (c_game_queue_chore_scan.Found)
        {
            c_game_queue_chore = Marshal.GetDelegateForFunctionPointer<c_game_queue_chore_delegate>((IntPtr)c_game_queue_chore_scan.CurrentAddress);
        }
        else LogHelper.Warning($"Failed to find c_game_queue_chore");

        DataScanner c_game_chore_transfer_field_scan = scanner.Scan(CompiledPattern.Parse("45 85 C0 0F 8E ? ? ? ? 48 89 5C 24 ? 57"));
        if (c_game_chore_transfer_field_scan.Found)
        {
            c_game_chore_transfer_field = Marshal.GetDelegateForFunctionPointer<c_game_chore_transfer_field_delegate>((IntPtr)c_game_chore_transfer_field_scan.CurrentAddress);
        }
        else LogHelper.Warning($"Failed to find c_game_chore_transfer_field");

        //tx.AddDetour(ref c_game_receive_chore_internal_hook,
        //    "48 89 5C 24 ? 57 48 83 EC ? 48 63 FA B8",
        //    c_game_receive_chore_internal_hook_impl);

        //tx.AddDetour(ref c_game_chore_pack_field_hook,
        //    "45 85 C0 0F 8E ? ? ? ? 48 89 5C 24 ? 57",
        //    c_game_chore_pack_field_hook_impl);

        // 106: Script Extender special chore handler
        unsafe
        {
            ((UInt64*)GameGlobalsManager.Instance.GameStateChoreHandlersVA)[106] = (UInt64)(Marshal.GetFunctionPointerForDelegate(c_game_chore_106_handler_impl));
        }

        tx.Commit();
    }

    // -------------------------------------------------------------------------
    // Script Extender chore (106): arbitrary payload transport
    // -------------------------------------------------------------------------

    public unsafe static void c_game_chore_106_handler_impl()
    {
        ChorePhase phase = *GameNetworkAPI.Instance.ChorePhase;
        IntPtr choreManager = (IntPtr)GameGlobalsManager.Instance.ChoreManagerVA;
        try
        {
            switch (phase)
            {
                case ChorePhase.Pack:
                    GameNetworkAPI.PackScriptExtenderChore();
                    break;
                case ChorePhase.Unpack:
                    GameNetworkAPI.UnpackScriptExtenderChore();
                    break;
                case ChorePhase.Measure:
                    GameNetworkAPI.MeasureScriptExtenderChore();
                    break;
                default:
                    *GameNetworkAPI.Instance.CurrentPayloadSize = 0;
                    LogHelper.Warning($"Chore 106 received unknown handler phase {(int)phase}");
                    break;
            }
        }
        catch (Exception ex)
        {
            *GameNetworkAPI.Instance.CurrentPayloadSize = 0;
            LogHelper.Error(ex, $"Chore 106 handler failed during phase {phase}.");
        }
    }

    //
    // void *__fastcall c_game_chore_transfer_field(__int64 pChoreManager, void *pField, int fieldSize, int bIsTickScheduled, int bIsUnpack)
    // 45 85 C0 0F 8E ? ? ? ? 48 89 5C 24 ? 57
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr c_game_chore_transfer_field_delegate(IntPtr pChoreManager, IntPtr pField, int fieldSize, int bIsTickScheduled, int bIsUnpack);

    public static c_game_chore_transfer_field_delegate c_game_chore_transfer_field;

    //internal static HookRef<X64ManagedFunctionDetourAOB<c_game_chore_pack_field_delegate>> c_game_chore_pack_field_hook = new();
    //public static IntPtr c_game_chore_pack_field_hook_impl(IntPtr pChoreManager, IntPtr pField, int fieldSize, int bIsTickScheduled, int bIsUnpack)
    //{
    //    LogHelper.Verbose($"pChoreManager={pChoreManager.ToString("X16")}, pField={pField.ToString("X16")}, fieldSize={fieldSize}, bIsTickScheduled={bIsTickScheduled}, bIsUnpack={bIsUnpack}");
    //    return c_game_chore_pack_field_hook.Value!.Hook!.Trampoline!(pChoreManager, pField, fieldSize, bIsTickScheduled, bIsUnpack);
    //}

    //
    // void *__fastcall c_game_queue_chore(__int64 pChoreManager, char choreId)
    // 48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 48 89 7C 24 ? 41 54 41 56 41 57 48 83 EC ? 0F BE F2
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void c_game_queue_chore_delegate(IntPtr pChoreManager, ChoreType chore);
    public static c_game_queue_chore_delegate c_game_queue_chore;
    //internal static HookRef<X64ManagedFunctionDetourAOB<c_game_queue_chore_delegate>> c_game_queue_chore_hook = new();
    //public static void c_game_queue_chore_hook_impl(IntPtr pChoreManager, ChoreType chore)
    //{
    //    LogHelper.Verbose($"pChoreManager={pChoreManager.ToString("X16")}, chore={chore}");
    //    c_game_queue_chore_hook.Value!.Hook!.Trampoline!(pChoreManager, chore);
    //}

    //
    // void *__fastcall c_game_receive_chore_internal(__int64 pChoreManager, unsigned int length, const void *data, char playerId)
    // 48 89 5C 24 ? 57 48 83 EC ? 48 63 FA B8
    //
    //[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    //public delegate IntPtr c_game_receive_chore_internal_delegate(IntPtr pChoreManager, int length, IntPtr data, byte playerId);
    //internal static HookRef<X64ManagedFunctionDetourAOB<c_game_receive_chore_internal_delegate>> c_game_receive_chore_internal_hook = new();
    //public static IntPtr c_game_receive_chore_internal_hook_impl(IntPtr pChoreManager, int length, IntPtr data, byte playerId)
    //{
    //    LogHelper.Verbose($"pChoreManager={pChoreManager.ToString("X16")}, length={length}, data={data.ToString("X16")}, playerId={playerId}");
    //    return c_game_receive_chore_internal_hook.Value!.Hook!.Trampoline!(pChoreManager, length, data, playerId);
    //}
}
