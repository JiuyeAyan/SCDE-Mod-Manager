using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Logging;
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace SHCDESE.Detours;

/// <summary>
/// Manages the installation of detours for game functions related to loading, saving, and managing map files.
/// This class is responsible for intercepting native game calls to inject custom logic, such as handling embedded map archives.
/// </summary>
[SuppressUnmanagedCodeSecurity]
internal unsafe class BulkMapLoaderDetours 
{
    private static bool _isMapEditorSave = false;
    private HookTransaction? tx;

    /// <summary>
    /// Scans the game memory for specific function patterns and applies hooks to them.
    /// This method should only be called once during the initialization process.
    /// </summary>
    /// <param name="memory">A ReadOnlySpan of the game executable memory to be scanned.</param>
    /// <param name="region">The memory region to scan.</param>
    public BulkMapLoaderDetours(ReadOnlySpan<byte> memory, ScanRegion region)
    {
        LogHelper.Information($"Applying");

        UInt64 currentImageBase = (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle;
        tx ??= new HookTransaction(region, Plugin.Instance.LoggerFactory);
        DataScanner scanner = DataScanner.Create(region);

        tx.AddDetour(c_game_dll_loadmaptoplay_hook, 
            HookTarget.FromExport("DLL_LoadMapToPlay", (IntPtr)currentImageBase),
            c_game_dll_loadmaptoplay_hook_impl);

        tx.AddDetour(c_game_unload_map_hook,
             "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 33 F6 48 8B D9",
             c_game_unload_map_hook_impl);

        tx.AddDetour(c_game_start_skirmish_game_handler,
             "48 8B C4 48 89 58 ? 55 56 57 41 54",
             c_game_start_skirmish_game_handler_hook_impl);

        tx.AddDetour(c_game_start_non_skirmish_game_handler,
             "48 89 5C 24 ? 55 56 57 41 54 41 56",
             c_game_start_non_skirmish_game_handler_hook_impl);

        tx.AddDetour(c_game_start_campaign_game_handler,
            "48 83 EC ? 8B 05 ? ? ? ? 66 0F 6F 05",
            c_game_start_campaign_game_handler_hook_impl);

        tx.AddDetour(c_game_dll_loadsavegame_hook, 
            HookTarget.FromExport("DLL_LoadSaveGame", (IntPtr)currentImageBase),
             c_game_dll_loadsavegame_hook_impl);

        tx.AddDetour(c_game_dll_savesavegame_hook, 
            HookTarget.FromExport("DLL_SaveSaveGame", (IntPtr)currentImageBase),
             c_game_dll_savesavegame_hook_impl);

        tx.AddContextHook(c_game_save_game_file,
            "8B CF E8 ?? ?? ?? ?? 48 8B 4B ?? E8",
            static (ctx) =>
            {
                UInt64 shcFileHandle = ctx.Pointer->RDI;
                try
                {
                    LogHelper.Information($"shcFileHandle={shcFileHandle}");

                    byte[]? mapArchiveBytes = GameMapArchiveManagerAPI.Instance.PrepareMapArchiveAndGetBytes(_isMapEditorSave);
                    if (mapArchiveBytes == null)
                    {
                        // this might mean we want to create a new one. check if the mapArchive is null.
                        if (GameMapArchiveManagerAPI.Instance.GetMapArchive() == null)
                        {
                            return;
                        }

                        // we want to create a fresh new zip archive.
                        mapArchiveBytes = GameMapArchiveManagerAPI.CreateEmptyZip();
                    }

                    LogHelper.Information($"shcFileHandle={shcFileHandle} - Embedded zip archive with len={mapArchiveBytes.Length}");
                    fixed (byte* ptr = mapArchiveBytes)
                    {
                        c_game_save_file_write!((int)shcFileHandle, (IntPtr)ptr, (uint)mapArchiveBytes.Length);
                        LogHelper.Information($"shcFileHandle={shcFileHandle} - Embedded zip archive!");
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Error(ex, $"shcFileHandle={shcFileHandle} - Error");
                }
                finally
                {
                    // Reset the flag after use
                    _isMapEditorSave = false;
                }
            }, new RedBird.X64.Hooks.Context.ContextHookOptions() { Registers = RedBird.X64.Assembly.X64SmartCPUContextRegs.Volatile | RedBird.X64.Assembly.X64SmartCPUContextRegs.RDI });


        if (scanner.Scan("E8 ? ? ? ? 41 0F B6 D7").TryReadFlowControlTarget(out UInt64 c_game_save_file_write_rva))
        {
            c_game_save_file_write = Marshal.GetDelegateForFunctionPointer<c_game_save_file_write_delegate>((IntPtr)(c_game_save_file_write_rva));
            LogHelper.Information($"c_game_save_file_write found at {(c_game_save_file_write_rva).ToString("X16")}");
        } 
        else LogHelper.Error($"Could not retrieve function ptr to c_game_save_file_write");

        tx.Commit();
    }

    internal static HookHandle<X64InlineHook> c_game_save_game_file = new();

    // E8 ?? ?? ?? ?? 0F B6 55  : int __cdecl c_game_save_file_write(int fileHandle, const void *buffer, unsigned int bufferLength)
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int c_game_save_file_write_delegate(int shcFileHandle, IntPtr buffer, UInt32 length);
    public static c_game_save_file_write_delegate? c_game_save_file_write;


    // __int64 __fastcall DLL_SaveSaveGame(wchar_t *data, int length, int screenCentreX, int screenCentreY, int realScreenCentreX, int realScreenCentreY, char lockMap, char tempLockOnly, char mapSave)
    // 48 89 5C 24 ? 55 56 57 48 81 EC ? ? ? ? 48 8B 05 ? ? ? ? 48 33 C4 48 89 84 24
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_dll_savesavegame_delegate(IntPtr data, int length, int screenCentreX, int screenCentreY, int realScreenCentreX, int realScreenCentreY, byte lockMap, byte tempLockOnly, byte mapSave);
    internal static DetourHandle<c_game_dll_savesavegame_delegate> c_game_dll_savesavegame_hook = new();
    public static Int64 c_game_dll_savesavegame_hook_impl(IntPtr data, int length, int screenCentreX, int screenCentreY, int realScreenCentreX, int realScreenCentreY, byte lockMap, byte tempLockOnly, byte mapSave)
    {
        try
        {
            // The mapSave parameter tells us if this is a map editor save
            _isMapEditorSave = (mapSave != 0);

            // Extract the file path from the data pointer
            string? filePath = null;
            if (data != IntPtr.Zero && length > 0)
            {
                filePath = new string((char*)data, 0, length / 2).TrimEnd('\0');
            }

            // Determine if this is a save file based on mapSave parameter
            // mapSave=0 means in-game save (.sav), mapSave=1 means map editor save (.map)
            bool isSaveFile = (mapSave == 0);

            LogHelper.Information($"Save game called - mapSave={mapSave}, isMapEditorSave={_isMapEditorSave}, isSaveFile={isSaveFile}, filePath={filePath}");

            // Set the file context so GameMapArchiveManagerAPI knows what we're saving
            if (!string.IsNullOrEmpty(filePath))
            {
                GameMapArchiveManagerAPI.Instance.SetCurrentFileContext(filePath!, isSaveFile);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error while saving save game");
        }
        return c_game_dll_savesavegame_hook.Original(data, length, screenCentreX, screenCentreY, realScreenCentreX, realScreenCentreY, lockMap, tempLockOnly, mapSave);
    }

    // __int64 __fastcall DLL_LoadSaveGame(wchar_t *data, int length, __int64 retData, char bLoadingEditorMap)
    // 40 53 55 56 57 41 56 48 81 EC
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_dll_loadsavegame_delegate(IntPtr fileName, int length, IntPtr retData, byte bLoadingEditorMap);
    internal static DetourHandle<c_game_dll_loadsavegame_delegate> c_game_dll_loadsavegame_hook = new();
    public static Int64 c_game_dll_loadsavegame_hook_impl(IntPtr fileName, int length, IntPtr retData, byte bLoadingEditorMap)
    {
        try
        {
            string safeFileName = new string((char*)fileName, 0, length / 2).TrimEnd('\0');
            LogHelper.Information($"safeFileName={safeFileName}, length={length}, retData={retData.ToString("X16")}, bLoadingEditorMap={bLoadingEditorMap}");

            LoadSaveGameEventArgs eventArgs = new(EventHookPhase.Pre, safeFileName, retData, bLoadingEditorMap == 1);
            MapLoaderR3EventHooks.OnLoadSave.Raise(eventArgs);
            Int64 originalResult = c_game_dll_loadsavegame_hook.Original(fileName, length, retData, bLoadingEditorMap);
            LoadSaveGameEventArgs postEventArgs = new(EventHookPhase.Post, safeFileName, retData, bLoadingEditorMap == 1)
            {
                ReturnValue = originalResult
            };
            MapLoaderR3EventHooks.OnLoadSave.Raise(postEventArgs);
            return originalResult;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error while loading save game");
        }
        return c_game_dll_loadsavegame_hook.Original(fileName, length, retData, bLoadingEditorMap);
    }

    /// <summary>
    /// This bool prevents R3 notification from the map unload event.
    /// This is a bandaid fix to avoid <see cref="c_game_start_skirmish_game_handler_hook_impl"/> booting up a 
    /// lua VM just for it to be immediately erased by its own call to <see cref="c_game_unload_map_hook_impl"/>
    /// </summary>
    private static bool _preventUnloadingNotification = false;

    // 48 89 5C 24 ? 48 89 74 24 ? 57 48 83 EC ? 33 F6 48 8B D9 : __int64 __fastcall c_game_unload_map(__int64 pTileManager)
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate UInt64 c_game_unload_map_delegate(IntPtr pTileManager);
    internal static DetourHandle<c_game_unload_map_delegate> c_game_unload_map_hook = new();
    public static UInt64 c_game_unload_map_hook_impl(IntPtr pTileManager)
    {
        LogHelper.Information($"pTileManager={pTileManager.ToString("X16")}, preventNotification={_preventUnloadingNotification}");
        if (_preventUnloadingNotification)
        {
            return c_game_unload_map_hook.Original(pTileManager);
        }

        try
        {
            MapUnloadEventArgs eventArgs = new(EventHookPhase.Pre, pTileManager);
            MapLoaderR3EventHooks.OnUnloadMap.Raise(eventArgs);
            UInt64 originalResult = c_game_unload_map_hook.Original(pTileManager);
            eventArgs.ReturnValue = originalResult;
            MapUnloadEventArgs postEventArgs = new(EventHookPhase.Post, pTileManager)
            {
                ReturnValue = originalResult
            };
            MapLoaderR3EventHooks.OnUnloadMap.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
            return eventArgs.ReturnValue;
        } 
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error while unloading map");
        }
        return c_game_unload_map_hook.Original(pTileManager);
    }

    // 48 83 EC ? 8B 05 ? ? ? ? 66 0F 6F 05 : __int64 __fastcall c_game_start_campaign_game_handler(int campaignMapId)
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate UInt64 c_game_start_campaign_game_handler_delegate(int campaignMapId);
    internal static DetourHandle<c_game_start_campaign_game_handler_delegate> c_game_start_campaign_game_handler = new();
    public unsafe static UInt64 c_game_start_campaign_game_handler_hook_impl(int campaignMapId)
    {
        LogHelper.Information($"campaignMapId={campaignMapId}");

        try
        {
            MapStartEventArgs eventArgs = new(EventHookPhase.Pre, IntPtr.Zero, 0, 0, campaignMapId);
            MapLoaderR3EventHooks.OnStartMap.Raise(eventArgs);

            _preventUnloadingNotification = true;
            UInt64 originalResult = c_game_start_campaign_game_handler.Original(campaignMapId);
            _preventUnloadingNotification = false;

            eventArgs.ReturnValue = originalResult;
            MapStartEventArgs postEventArgs = new(EventHookPhase.Post, IntPtr.Zero, 0, 0, campaignMapId)
            {
                ReturnValue = originalResult
            };
            MapLoaderR3EventHooks.OnStartMap.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
            return eventArgs.ReturnValue;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error while executing start game handler");
        }
        return c_game_start_campaign_game_handler.Original(campaignMapId);
    }

    // 48 8B C4 48 89 58 ? 55 56 57 41 54 : __int64 __fastcall c_game_start_skirmish_game_handler(__int16 *a1, char bMultiplayerSave, __int64 a3)
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate UInt64 c_game_start_skirmish_game_handler_delegate(IntPtr a1, byte bMultiplayerSave, UInt64 a3);
    internal static DetourHandle<c_game_start_skirmish_game_handler_delegate> c_game_start_skirmish_game_handler = new();
    public unsafe static UInt64 c_game_start_skirmish_game_handler_hook_impl(IntPtr a1, byte bMultiplayerSave, UInt64 a3)
    {
        LogHelper.Information($"a1={a1.ToString("X16")}, bMultiplayerSave={bMultiplayerSave}, a3={a3}");

        try
        {
            MapStartEventArgs eventArgs = new(EventHookPhase.Pre, a1, bMultiplayerSave, a3);
            MapLoaderR3EventHooks.OnStartMap.Raise(eventArgs);

            _preventUnloadingNotification = true;
            UInt64 originalResult = c_game_start_skirmish_game_handler.Original(
                eventArgs.Unknown1,
                eventArgs.bMultiplayerSave,
                eventArgs.Unknown3
            );
            _preventUnloadingNotification = false;

            eventArgs.ReturnValue = originalResult;
            MapStartEventArgs postEventArgs = new(EventHookPhase.Post, a1, bMultiplayerSave, a3)
            {
                ReturnValue = originalResult
            };
            MapLoaderR3EventHooks.OnStartMap.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
            return eventArgs.ReturnValue;
        } 
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error while executing start game handler");
        }
        return c_game_start_skirmish_game_handler.Original(a1, bMultiplayerSave, a3);
    }

    // 48 89 5C 24 ? 55 56 57 41 54 41 55 48 83 EC ? 48 8B F1 : __int64 __fastcall c_game_start_non_skirmish_game_handler(char *a1, __int64 a2)
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate UInt64 c_game_start_non_skirmish_game_handler_delegate(IntPtr a1, UInt64 a2);
    internal static DetourHandle<c_game_start_non_skirmish_game_handler_delegate> c_game_start_non_skirmish_game_handler = new();
    public unsafe static UInt64 c_game_start_non_skirmish_game_handler_hook_impl(IntPtr a1, UInt64 a2)
    {
        LogHelper.Information($"a1={a1.ToString("X16")}, a2={a2}");

        try
        {
            MapStartEventArgs eventArgs = new(EventHookPhase.Pre, a1, 0, a2);
            MapLoaderR3EventHooks.OnStartMap.Raise(eventArgs);

            _preventUnloadingNotification = true;
            UInt64 originalResult = c_game_start_non_skirmish_game_handler.Original(
                eventArgs.Unknown1,
                eventArgs.Unknown3
            );
            _preventUnloadingNotification = false;

            eventArgs.ReturnValue = originalResult;
            MapStartEventArgs postEventArgs = new(EventHookPhase.Post, a1, 0, a2)
            {
                ReturnValue = originalResult
            };
            MapLoaderR3EventHooks.OnStartMap.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
            return eventArgs.ReturnValue;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error while executing start game handler");
        }
        return c_game_start_non_skirmish_game_handler.Original(a1, a2);
    }

    // __int64 __fastcall DLL_LoadMapToPlay(unsigned int campaignMapID, wchar_t *fileName, int length, __int64 retData, int dummy, wchar_t *mapName, int maplength, unsigned __int8 multiplayerSave, int trailType, int trailID, unsigned __int8 allow_classic_bedouins)
    // 40 53 55 56 57 41 54 41 55 41 56 41 57 48 81 EC ? ? ? ? 48 8B 05 ? ? ? ? 48 33 C4 48 89 84 24 ? ? ? ? 48 8B 84 24
    // NOTE: The StringBuilders act twofold: wchar_t* and will parse the next arg as a length, so they are wchar*_t and int length.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate UInt64 c_game_dll_loadmaptoplay_delegate(UInt32 campaignMapID, IntPtr fileName, int length, IntPtr retData, int dummy, IntPtr mapName, int maplength, byte multiplayerSave, int trailType, int trailID, byte allow_classic_bedouins);
    public static DetourHandle<c_game_dll_loadmaptoplay_delegate> c_game_dll_loadmaptoplay_hook = new();
    public unsafe static UInt64 c_game_dll_loadmaptoplay_hook_impl(UInt32 campaignMapID, IntPtr fileName, int length, IntPtr retData, int dummy, IntPtr mapName, int maplength, byte multiplayerSave, int trailType, int trailID, byte allow_classic_bedouins)
    {
        try
        {
            string safeFileName = new string((char*)fileName, 0, length / 2).TrimEnd('\0');
            string safeMapName = new string((char*)mapName, 0, maplength / 2).TrimEnd('\0');
            LogHelper.Information($"campaignMapID={campaignMapID}, safeFileName={safeFileName}, length={length}, retData={retData.ToString("X16")}, dummy={dummy}, safeMapName={safeMapName}, maplength={maplength}, multiplayerSave={multiplayerSave}, trailType={trailType}, trailID={trailID}, allow_classic_bedouins={allow_classic_bedouins}");

            MapLoadEventArgs eventArgs = new(EventHookPhase.Pre, campaignMapID, safeFileName, retData, safeMapName, multiplayerSave, trailType, trailID, allow_classic_bedouins);
            MapLoaderR3EventHooks.OnLoadMap.Raise(eventArgs);
            UInt64 originalResult = c_game_dll_loadmaptoplay_hook.Original(campaignMapID, fileName, length, retData, dummy, mapName, maplength, multiplayerSave, trailType, trailID, allow_classic_bedouins);

            MapLoadEventArgs postEventArgs = new(EventHookPhase.Post, campaignMapID, safeFileName, retData, safeMapName, multiplayerSave, trailType, trailID, allow_classic_bedouins)
            {
                ReturnValue = originalResult
            };
            MapLoaderR3EventHooks.OnLoadMap.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
            return eventArgs.ReturnValue;
        } 
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error while loading map to play");
        }
        return c_game_dll_loadmaptoplay_hook.Original(campaignMapID, fileName, length, retData, dummy, mapName, maplength, multiplayerSave, trailType, trailID, allow_classic_bedouins);
    }
}
