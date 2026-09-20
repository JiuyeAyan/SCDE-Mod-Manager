using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.Core.Memory.Scanners;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.Logging;
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace SHCDESE.Detours;

[SuppressUnmanagedCodeSecurity]
internal class BulkSoundDetours
{
    private HookTransaction? tx;
    public BulkSoundDetours(ReadOnlySpan<byte> memory, ScanRegion region)
    {
        LogHelper.Information($"Applying");

        UInt64 currentImageBase = (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle;
        tx ??= new HookTransaction(region, Plugin.Instance.LoggerFactory);
        DataScanner scanner = DataScanner.Create(region);

        // c_game_soundmanager_play_sound: 48 89 5C 24 ?? 57 48 83 EC ?? 83 79
        DataScanner c_game_soundmanager_play_mono_sound_scan = scanner.Scan(CompiledPattern.Parse("48 89 5C 24 ?? 57 48 83 EC ?? 83 79"));
        if (c_game_soundmanager_play_mono_sound_scan.Found)
        {
            c_game_soundmanager_play_mono_sound = Marshal.GetDelegateForFunctionPointer<c_game_soundmanager_play_mono_sound_delegate>((IntPtr)c_game_soundmanager_play_mono_sound_scan.CurrentAddress);
        }
        else LogHelper.Warning($"Failed to find c_game_update_visual_goodsyard_goods");

        tx.AddDetour(c_game_soundmanager_play_speech_sound_hook,
            HookTarget.FromRelativeCall("E8 ? ? ? ? E9 ? ? ? ? 44 8B C1"),
            c_game_soundmanager_play_speech_sound_hook_impl);

        tx.Commit();
    }

    //
    // int __fastcall c_game_soundmanager_play_speech_sound(__int64 pSoundManager, const char *file_name)
    // RelativeCall E8 ? ? ? ? E9 ? ? ? ? 44 8B C1
    // call from c_game_action_marketplace_interact
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int c_game_soundmanager_play_speech_sound_delegate(IntPtr pSoundManager, IntPtr fileName);
    internal static DetourHandle<c_game_soundmanager_play_speech_sound_delegate> c_game_soundmanager_play_speech_sound_hook = new();
    public static int c_game_soundmanager_play_speech_sound_hook_impl(IntPtr pSoundManager, IntPtr fileName)
    {
        //LogHelper.Verbose($"pSoundManager={pSoundManager.ToString("X16")}, fileName={fileName.ToString("X16")}");

        int ret = 0;
        try
        {
            GameSoundManagerAPI soundApi = GameSoundManagerAPI.Instance;
            if (soundApi.GetSuppressSpeech())
            {
                return ret;
            } else ret = c_game_soundmanager_play_speech_sound_hook.Original(pSoundManager, fileName);
        } 
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error during attempted sound suppression");
            return c_game_soundmanager_play_speech_sound_hook.Original(pSoundManager, fileName);
        }
        return ret;
    }

    // void __fastcall c_game_soundmanager_play_mono_sound(__int64 pSoundManager, char *file_path)
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void c_game_soundmanager_play_mono_sound_delegate(IntPtr pSoundManager, string filePath);
    public static c_game_soundmanager_play_mono_sound_delegate c_game_soundmanager_play_mono_sound;

}
