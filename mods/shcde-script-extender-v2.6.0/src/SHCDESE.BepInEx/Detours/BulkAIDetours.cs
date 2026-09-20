using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.AI;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using SHCDESE.Lua;
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace SHCDESE.Detours;

public unsafe class BulkAIDetours
{
    private HookTransaction? tx;

    public BulkAIDetours(ReadOnlySpan<byte> memory, ScanRegion region)
    {
        LogHelper.Information($"Applying");
        UInt64 currentImageBase = (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle;

        tx ??= new HookTransaction(region, Plugin.Instance.LoggerFactory);

        tx.AddDetour(c_game_ai_enqueue_message_hook,
            "83 39 ? 0F 84 ? ? ? ? 48 63 81 ? ? ? ? 83 F8 ? 0F 84 ? ? ? ? C7 84 81",
            c_game_ai_enqueue_message_hook_impl);

        tx.AddDetour(c_game_ai_enqueue_message_wrapper_hook,
            "4C 8B DC 55 56 41 56",
            c_game_ai_enqueue_message_wrapper_hook_impl);

        tx.AddDetour(c_game_dll_importaiv_hook,
            HookTarget.FromExport("DLL_ImportAIV"),
            c_game_dll_importaiv_hook_impl);
        
        tx.AddDetour(c_game_ai_build_wall_hook,
            "48 89 5C 24 ? 48 89 6C 24 ? 56 57 41 56 48 83 EC ? 0F B7 5C 24",
            c_game_ai_build_wall_hook_impl);
        
        //
        // Allows custom siege rallypoint selection logic. See (c_game_ai_setup_siege_pathing_stuff)
        //
        tx.AddContextHook(c_game_ai_setup_siege_pathing_stuff_hook,
            HookTarget.FromPattern("BA ? ? ? ? E8 ? ? ? ? 48 8B 5C 24 ? 48 8B 6C 24 ? 48 8B 74 24 ? 48 83 C4 ? 41 5F 41 5E 5F", offset: 5),
            static ctx =>
            {
                int maxSearchRange = (int)ctx.Pointer->RDX;
                int preferredStandoffDistance = (int)ctx.Pointer->R8;
                int pathConnectionLayer = (int)ctx.Pointer->R9;
                UInt64* pPlayerId = ctx.Pointer->GetStackPtr<UInt64>(0);
                UInt64 playerId = *pPlayerId;

                LogHelper.Debug($"c_game_ai_setup_siege_pathing_stuff_hook, maxSearchRange={maxSearchRange}, preferredStandoffDist={preferredStandoffDistance}, pathConnectionLayer={pathConnectionLayer}, playerId={playerId}");

                AISelectSiegeRallypointEventArgs eventArgs = new(EventHookPhase.Pre, maxSearchRange, preferredStandoffDistance, pathConnectionLayer, (int)playerId);
                AIR3EventHooks.OnAISelectSiegeRallypoint.Raise(eventArgs);

                ctx.Pointer->RDX = (UInt64)eventArgs.MaxSearchRange;
                ctx.Pointer->R8 = (UInt64)eventArgs.PreferredStandoffDistance;
                ctx.Pointer->R9 = (UInt64)eventArgs.PathConnectionLayer;
                *pPlayerId = (UInt64)eventArgs.PlayerId;
            }, new RedBird.X64.Hooks.Context.ContextHookOptions() { Registers = RedBird.X64.Assembly.X64SmartCPUContextRegs.Volatile });

        tx.AddDetour<c_game_ai_should_not_build_hovel_hook_delegate>("41 83 F8 ? 75 ? 48 63 C2 48 8D 15 ? ? ? ? 48 69 C8",
            c_game_ai_should_not_build_hovel_hook_impl);

        tx.Commit();
    }

    public static HookHandle<X64InlineHook> c_game_ai_setup_siege_pathing_stuff_hook = new();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool c_game_ai_should_not_build_hovel_hook_delegate(IntPtr pLordManager, int playerId, eMappers eMappers);
    public static bool c_game_ai_should_not_build_hovel_hook_impl(IntPtr pLordManager, int playerId, eMappers eMappers)
    {
        try
        {
            LogHelper.Verbose($"playerId={playerId}, eMappers={eMappers}");

            AIQueryBuildHovelEventArgs eventArgs = new AIQueryBuildHovelEventArgs(EventHookPhase.Pre, playerId, eMappers);
            AIR3EventHooks.OnAIQueryBuildHovelEventArgs.Raise(eventArgs);

            if (eventArgs.SkipOriginalFunction)
                return eventArgs.ReturnValue;

            if (eMappers != eMappers.MAPPER_HOVEL)
                return false;

            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId, out GamePlayerResources* playerResource))
            {
                LogHelper.Warning($"Could not find playerresource by id: {playerId}");
                return false;
            }

            int civilianHousingSpace = (int)playerResource->r_CivilianHousingSpace;
            if (civilianHousingSpace > 12 && (playerResource->r_TotalPopulation < civilianHousingSpace || playerResource->r_IdlePeasantCurrent >= 5 || playerResource->r_IdlePeasantAverage >= 5 || playerResource->r_CurrentPopularity < 5000))
                return true;

        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception during build wall handling for ai");
        }
        return false;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void c_game_ai_build_wall_delegate(IntPtr pTileManager, int playerId, int tileX, int tileY, eMappers eMappers);
    internal static DetourHandle<c_game_ai_build_wall_delegate> c_game_ai_build_wall_hook = new();
    public static void c_game_ai_build_wall_hook_impl(IntPtr pTileManager, int playerId, int tileX, int tileY, eMappers eMappers)
    {
        AIBuildWallEventArgs? eventArgs = null;

        try
        {
            LogHelper.Verbose($"playerId={playerId}, tileX={tileX}, tileY={tileY}, eMappers={eMappers}");

            eventArgs = new AIBuildWallEventArgs(EventHookPhase.Pre, playerId, tileX, tileY, eMappers);
            AIR3EventHooks.OnAIBuildWall.Raise(eventArgs);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception during build wall handling for ai");
        }
        finally
        {
            c_game_ai_build_wall_hook.Original(pTileManager,
                eventArgs?.PlayerId ?? playerId,
                eventArgs?.TileX ?? tileX,
                eventArgs?.TileY ?? tileY,
                eventArgs?.Mappers ?? eMappers);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void c_game_dll_importaiv_delegate(int playerId, int aiLord, UInt64 aivData, int length);
    internal static DetourHandle<c_game_dll_importaiv_delegate> c_game_dll_importaiv_hook = new();
    public static void c_game_dll_importaiv_hook_impl(int playerId, int aiLord, UInt64 aivData, int length)
    {
        LogHelper.Information($"Loaded Internal AIV for lord {(Enums.AILords)aiLord}, playerId={playerId} with length={length}");
        //System.IO.File.WriteAllBytes("aiv.bin", new Span<byte>((void*)aivData, length * 2).ToArray());
        c_game_dll_importaiv_hook.Original(playerId, aiLord, aivData, length);
        try
        {
            playerId += 1;
            string lordName = GameAIManagerAPI.Instance.GetCustomAILordNameByPlayerId(playerId);
            if (GameAIManagerAPI.Instance.IsSupportedCustomLord(lordName) && GameAIManagerAPI.Instance.TryGetLuaInitPath(lordName, out string? luaInitPath))
            {
                LuaManager.Instance.RegisterLordAI(lordName, luaInitPath, playerId);
            }
        } catch (Exception ex)
        {
            LogHelper.Error(ex, "Error during importaiv callback");
        }
    }

    // __int64 __fastcall c_game_ai_enqueue_message_wrapper(_DWORD *pMessageManager, unsigned int playerId, int eAILords, int msgType)
    // 4C 8B DC 55 56 41 56
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate UInt64 c_game_ai_enqueue_message_wrapper_delegate(UInt64 pMessageManager, UInt64 playerId, AILords aiLord, UInt64 msgType);
    internal static DetourHandle<c_game_ai_enqueue_message_wrapper_delegate> c_game_ai_enqueue_message_wrapper_hook = new();
    public static UInt64 c_game_ai_enqueue_message_wrapper_hook_impl(UInt64 pMessageManager, UInt64 playerId, AILords aiLord, UInt64 msgType)
    {
        LogHelper.Debug($"c_game_ai_enqueue_message_wrapper_hook_impl, pMessageManager={pMessageManager.ToString("X16")}, playerId={playerId}, aiLord={aiLord}, msgType={msgType}");

        UInt64 ret = 0;
        try
        {
            GameSoundManagerAPI soundApi = GameSoundManagerAPI.Instance;
            if (soundApi.GetSuppressMessages())
            {
                return ret;
            }
            else ret = c_game_ai_enqueue_message_wrapper_hook.Original(pMessageManager, playerId, aiLord, msgType);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error during attempted message suppression");
            return c_game_ai_enqueue_message_wrapper_hook.Original(pMessageManager, playerId, aiLord, msgType);
        }
        return ret;

    }

    // __int64 __fastcall c_game_ai_enqueue_message(_DWORD *pMessageManager, int a2, int msgType, char *pVideoPath, char *pAudioPath, unsigned int playerId)
    // 83 39 ? 0F 84 ? ? ? ? 48 63 81 ? ? ? ? 83 F8 ? 0F 84 ? ? ? ? C7 84 81
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate UInt64 c_game_ai_enqueue_message_delegate(UInt64 pMessageManager, UInt64 a2, UInt64 msgType, UInt64 pVideoPath, UInt64 pAudioPath, int playerId);
    internal static DetourHandle<c_game_ai_enqueue_message_delegate> c_game_ai_enqueue_message_hook = new();
    public static UInt64 c_game_ai_enqueue_message_hook_impl(UInt64 pMessageManager, UInt64 a2, UInt64 msgType, UInt64 pVideoPath, UInt64 pAudioPath, int playerId)
    {
        IntPtr customVideoPtr = IntPtr.Zero;
        IntPtr customAudioPtr = IntPtr.Zero;
        try
        {
            LogHelper.Debug($"pMessageManager={pMessageManager.ToString("X16")}, a2={a2}, msgType={msgType}, pVideoPath={pVideoPath.ToString("X16")}, pAudioPath={pAudioPath.ToString("X16")}, playerId={playerId}");
            string pVideoPathEx = Marshal.PtrToStringAnsi((IntPtr)pVideoPath);
            string pAudioPathEx = Marshal.PtrToStringAnsi((IntPtr)pAudioPath);
            LogHelper.Debug($"VideoPath: [{pVideoPathEx}], AudioPath: [{pAudioPathEx}]");

            Enums.AILords lord = GamePlayerManagerAPI.Instance.GetAILord(Math.Abs(playerId));

            if (GameAIManagerAPI.Instance.TryGetMessageTypeFromIndex((int)msgType, out AILordMessageType msgTypeEnum))
            {
                // int rawIndex = (int)msgType - 1;
                // This tells who the game "thinks" the lord is internally (e.g., 7 = Sultan)
                // int internalLordId = (rawIndex / 34) + 1;
                string lordName = "not-found";
                lordName = GameAIManagerAPI.Instance.GetCustomAILordNameByPlayerId(Math.Abs(playerId));
                LogHelper.Debug($"Category: {msgTypeEnum} RealLord: {lord}, lordName={lordName}");

                switch (lord)
                {
                    case Enums.AILords.SK_X1:
                    case Enums.AILords.SK_X2:
                    case Enums.AILords.SK_X3:
                    case Enums.AILords.SK_X4:
                    case Enums.AILords.SK_X5:
                    case Enums.AILords.SK_X6:
                    case Enums.AILords.SK_X7:
                    case Enums.AILords.SK_X8:
                        if (GameAIManagerAPI.Instance.TryGetVideoAndAudio(lordName, msgTypeEnum, out string vid, out string aud))
                        {
                            LogHelper.Information($"new vid={vid}, new aud={aud}");

                            if (!string.IsNullOrEmpty(vid))
                            {
                                customVideoPtr = Marshal.StringToHGlobalAnsi(vid);
                                pVideoPath = (UInt64)customVideoPtr;
                            }

                            if (!string.IsNullOrEmpty(aud))
                            {
                                customAudioPtr = Marshal.StringToHGlobalAnsi(aud);
                                pAudioPath = (UInt64)customAudioPtr;
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
            return c_game_ai_enqueue_message_hook.Original(pMessageManager, a2, msgType, pVideoPath, pAudioPath, playerId);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception");

            return c_game_ai_enqueue_message_hook.Original(pMessageManager, a2, msgType, pVideoPath, pAudioPath, playerId);
        }
        finally
        {
            if (customVideoPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(customVideoPtr);
            if (customAudioPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(customAudioPtr);
        }
    }
}
