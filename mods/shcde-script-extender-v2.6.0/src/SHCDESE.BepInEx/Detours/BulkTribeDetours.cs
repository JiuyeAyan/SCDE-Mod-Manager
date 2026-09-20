using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using Serilog;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using System;
using System.Runtime.InteropServices;
namespace SHCDESE.Detours;

public unsafe class BulkTribeDetours
{
    private HookTransaction? tx;
    public BulkTribeDetours(ReadOnlySpan<byte> memory, ScanRegion region)
    {
        LogHelper.Information($"Applying");

        UInt64 currentImageBase = (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle;
        tx ??= new HookTransaction(region, Plugin.Instance.LoggerFactory);

        tx.AddDetour(c_game_unit_assign_tribe_hook,
            "48 89 5C 24 ?? 57 48 63 DA",
             c_game_unit_assign_tribe_hook_impl);

        tx.AddDetour(c_game_create_new_tribe_hook,
             HookTarget.FromRelativeCall("E8 ?? ?? ?? ?? 48 63 F8 89 7C 24 ?? 85 C0"),
             c_game_create_new_tribe_hook_impl);
        
        tx.AddDetour(c_game_tribe_issueorder_movehere_hook,
            "48 89 5C 24 ?? 89 54 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 81 EC",
            c_game_tribe_issueorder_movehere_hook_impl);
        
        tx.AddDetour(c_game_tribe_issueorder_withtarget_hook,
            "44 89 4C 24 ? 44 89 44 24 ? 89 54 24 ? 48 89 4C 24 ? 53 55 56 57 41 54 41 55 41 56 41 57 48 81 EC ? ? ? ? 33 F6",
            c_game_tribe_issueorder_withtarget_hook_impl);
        
        tx.AddDetour(c_game_tribe_delete,
            "48 63 C2 4C 8D 49",
            c_game_tribe_delete_hook_impl);
        
        tx.AddDetour(c_game_spawn_unit_group_eu_and_arab,
            "48 89 5C 24 ? 48 89 6C 24 ? 44 89 4C 24 ? 56",
            c_game_spawn_unit_group_eu_and_arab_hook_impl);
        
        tx.AddDetour(c_game_tribe_remove_unit,
            "48 89 5C 24 ? 48 89 74 24 ? 48 89 7C 24 ? 41 56 48 83 EC ? 4C 63 CA",
            c_game_tribe_remove_unit_hook_impl);
        
        tx.AddContextHook(c_game_tribe_get_next_waypoint,
             "49 63 C0 49 69 D5",
             static ctx =>
             {
                 UInt64 tribeAddr = ctx.Pointer->RBX;
                 UInt64 nextWaypointIndex = ctx.Pointer->R8;
                 int tribeId = (int)((tribeAddr - (UInt64)GameTribeManagerAPI.Instance._tribesArray._array) / (UInt64)sizeof(GameTribe)) + 1;

                 Log.Debug($"c_game_tribe_get_next_waypoint: tribeId={tribeId}, nextWaypointIndex={nextWaypointIndex}");
                 TribeGetNextPatrolWaypointEventArgs eventArgs = new(EventHookPhase.Pre, tribeId, (int)nextWaypointIndex);
                 TribeR3EventHooks.OnTribeGetNextPatrolWaypoint.Raise(eventArgs);

                 ctx.Pointer->R8 = (UInt64)eventArgs.PatrolPointIndex;

             }, new RedBird.X64.Hooks.Context.ContextHookOptions() { Registers = RedBird.X64.Assembly.X64SmartCPUContextRegs.Volatile | RedBird.X64.Assembly.X64SmartCPUContextRegs.RBX });

        tx.Commit();
    }

    internal static HookHandle<X64InlineHook> c_game_tribe_get_next_waypoint = new();

    // __int64 __fastcall c_game_tribe_remove_unit(__int64 pTribeManager, int unitId, int tribeId)
    // 48 89 5C 24 ? 48 89 74 24 ? 48 89 7C 24 ? 41 56 48 83 EC ? 4C 63 CA
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_tribe_remove_unit_delegate(NativePointer<GameTribeManager> manager, int unitId, int tribeId);
    internal static DetourHandle<c_game_tribe_remove_unit_delegate> c_game_tribe_remove_unit = new();
    public unsafe static Int64 c_game_tribe_remove_unit_hook_impl(NativePointer<GameTribeManager> manager, int unitId, int tribeId)
    {
        LogHelper.Verbose($"manager={((UInt64)manager.Pointer).ToString("X16")}, unitId={unitId}, tribeId={tribeId}");

        return c_game_tribe_remove_unit.Original(manager, unitId, tribeId);
    }

    // __int64 __fastcall c_game_spawn_unit_group_eu_and_arab(int *pTribeManager, __int16 unknownTribeArgument_0x5E4, __int16 unknownTribeArgument_0x18, int tileX, int tileY, int playerId, int unitType, int unitType2, int amount1, int amount2)
    // 48 89 5C 24 ? 48 89 6C 24 ? 44 89 4C 24 ? 56
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_spawn_unit_group_eu_and_arab_delegate(NativePointer<GameTribeManager> manager, UInt64 unknownTribeArgument_0x5E4, UInt64 unknownTribeArgument_0x18, int tileX, int tileY, int playerId, eChimps unitType1, eChimps unitType2, int amount1, int amount2);
    internal static DetourHandle<c_game_spawn_unit_group_eu_and_arab_delegate> c_game_spawn_unit_group_eu_and_arab = new();
    public unsafe static Int64 c_game_spawn_unit_group_eu_and_arab_hook_impl(NativePointer<GameTribeManager> manager, UInt64 unknownTribeArgument_0x5E4, UInt64 unknownTribeArgument_0x18, int tileX, int tileY, int playerId, eChimps unitType1, eChimps unitType2, int amount1, int amount2)
    {
        LogHelper.Verbose($"manager={((UInt64)manager.Pointer).ToString("X16")}, rdx={unknownTribeArgument_0x5E4}, r8={unknownTribeArgument_0x18}, tileX={tileX}, tileY={tileY}, playerId={playerId}, unitType1={unitType1}, unitType2={unitType2}, amount1={amount1}, amount2={amount2}");

        return c_game_spawn_unit_group_eu_and_arab.Original(manager, unknownTribeArgument_0x5E4, unknownTribeArgument_0x18, tileX, tileY, playerId, unitType1, unitType2, amount1, amount2);
    }

    // void *__fastcall c_game_tribe_delete(__int64 pTribeManager, int tribeId)
    // 48 63 C2 4C 8D 49
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_tribe_delete_delegate(NativePointer<GameTribeManager> manager, int tribeId);
    internal static DetourHandle<c_game_tribe_delete_delegate> c_game_tribe_delete = new();
    public unsafe static Int64 c_game_tribe_delete_hook_impl(NativePointer<GameTribeManager> manager, int tribeId)
    {
        LogHelper.Verbose($"manager={((UInt64)manager.Pointer).ToString("X16")}, tribeId={tribeId}");

        TribeDeleteEventArgs eventArgs = new(EventHookPhase.Pre, tribeId);
        TribeR3EventHooks.OnTribeDelete.Raise(eventArgs);

        Int64 originalResult = c_game_tribe_delete.Original(
            manager,
            eventArgs.TribeId
        );
        TribeDeleteEventArgs postEventArgs = new(EventHookPhase.Post, tribeId);
        TribeR3EventHooks.OnTribeDelete.Raise(postEventArgs);

        return originalResult;
    }

    // __int64 __fastcall c_game_tribe_issueorder_withtarget(__int64 pTribeManager, unsigned int tribeId, int aiCommandType, int target_value1, unsigned int target_value2, int a6)
    // 44 89 4C 24 ? 44 89 44 24 ? 89 54 24 ? 48 89 4C 24 ? 53 55 56 57 41 54 41 55 41 56 41 57 48 81 EC ? ? ? ? 8B BC 24
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_tribe_issueorder_withtarget_delegate(NativePointer<GameTribeManager> manager, int tribeId, TribeAICommand aiCommand, int targetValue1, int targetValue2, int a6);
    internal static DetourHandle<c_game_tribe_issueorder_withtarget_delegate> c_game_tribe_issueorder_withtarget_hook = new();
    public static Int64 c_game_tribe_issueorder_withtarget_hook_impl(NativePointer<GameTribeManager> manager, int tribeId, TribeAICommand aiCommand, int targetValue1, int targetValue2, int a6)
    {
        LogHelper.Debug($"manager={new IntPtr(manager).ToString("X16")}, tribeId={tribeId}, aiCommand={aiCommand}/{(int)aiCommand}, targetValue1={targetValue1}, targetValue2={targetValue2}, a6={a6}");

        TribeIssueOrderWithTargetEventArgs eventArgs = new(EventHookPhase.Pre, tribeId, aiCommand, targetValue1, targetValue2, a6);
        TribeR3EventHooks.OnTribeIssueOrderWithTarget.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_tribe_issueorder_withtarget_hook.Original(
                manager,
                eventArgs.TribeId,
                eventArgs.AICommand,
                eventArgs.TargetValue1,
                eventArgs.TargetValue2,
                eventArgs.a6
            );
            eventArgs.ReturnValue = originalResult;
            TribeIssueOrderWithTargetEventArgs postEventArgs = new(EventHookPhase.Post, tribeId, aiCommand, targetValue1, targetValue2, a6)
            {
                ReturnValue = originalResult
            };
            TribeR3EventHooks.OnTribeIssueOrderWithTarget.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    // __int64 __fastcall c_game_tribe_issueorder_movehere(__int64 pTribeManager, unsigned int tribeId, unsigned int tileX, unsigned int tileY, __int16 bIsPatrolPath, int, int)
    // 44 89 4C 24 ? 44 89 44 24 ? 89 54 24 ? 48 89 4C 24 ? 53 55 56 57 41 54 41 55 41 56 41 57 48 81 EC ? ? ? ? 8B BC 24
    // NOTES: a6 seems to be 1 whenever the player was the one to initate the command, perhaps bIssuedFromPlayer?
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_tribe_issueorder_movehere_delegate(NativePointer<GameTribeManager> manager, int tribeId, int tileX, int tileY, Int16 bIsPatrolPath, int bIsNewOrder, TribeMoveType tribeMoveType);
    internal static DetourHandle<c_game_tribe_issueorder_movehere_delegate> c_game_tribe_issueorder_movehere_hook = new();
    public static Int64 c_game_tribe_issueorder_movehere_hook_impl(NativePointer<GameTribeManager> manager, int tribeId, int tileX, int tileY, Int16 bIsPatrolPath, int bIsNewOrder, TribeMoveType tribeMoveType)
    {
        LogHelper.Debug($"manager={new IntPtr(manager).ToString("X16")}, tribeId={tribeId}, tileX={tileX}, tileY={tileY}, bIsPatrolPath={bIsPatrolPath}, bIsNewOrder={bIsNewOrder}, tribeMoveType={tribeMoveType}");

        TribeIssueOrderMoveHereEventArgs eventArgs = new(EventHookPhase.Pre, tribeId, tileX, tileY, bIsPatrolPath, bIsNewOrder == 1, tribeMoveType);
        TribeR3EventHooks.OnTribeIssueOrderMoveHere.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_tribe_issueorder_movehere_hook.Original(
                manager,
                eventArgs.TribeId,
                eventArgs.TileX,
                eventArgs.TileY,
                eventArgs.IsPatrolPath,
                eventArgs.IsNewOrder ? 1 : 0,
                eventArgs.MoveType
            );
            eventArgs.ReturnValue = originalResult;
            TribeIssueOrderMoveHereEventArgs postEventArgs = new(EventHookPhase.Post, tribeId, tileX, tileY, bIsPatrolPath, bIsNewOrder == 1, tribeMoveType)
            {
                ReturnValue = originalResult
            };
            TribeR3EventHooks.OnTribeIssueOrderMoveHere.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    // __int64 __fastcall c_game_unit_assign_tribe(__int64 pTribeManager, int unitId, int tribeId)
    // 48 89 5C 24 ?? 57 48 63 DA
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_unit_assign_tribe_delegate(NativePointer<GameTribeManager> manager, int unitId, int tribeId);
    internal static DetourHandle<c_game_unit_assign_tribe_delegate> c_game_unit_assign_tribe_hook = new();
    public static Int64 c_game_unit_assign_tribe_hook_impl(NativePointer<GameTribeManager> manager, int unitId, int tribeId)
    {
        LogHelper.Debug($"pTribeManager={new IntPtr(manager).ToString("X16")}, unitId={unitId}, tribeId={tribeId}");

        TribeAssignUnitEventArgs eventArgs = new(EventHookPhase.Pre, tribeId, unitId);
        TribeR3EventHooks.OnTribeAssignUnit.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_unit_assign_tribe_hook.Original(
                manager,
                eventArgs.UnitId,
                eventArgs.TribeId
            );
            eventArgs.ReturnValue = originalResult;
            TribeAssignUnitEventArgs postEventArgs = new(EventHookPhase.Post, tribeId, unitId)
            {
                ReturnValue = originalResult
            };
            TribeR3EventHooks.OnTribeAssignUnit.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    //  __int64 __fastcall c_game_create_new_tribe(int* pTribeManager, int a2, int a3)
    // E8 ?? ?? ?? ?? 48 63 F8 89 7C 24 ?? 85 C0
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_create_new_tribe_delegate(NativePointer<GameTribeManager> manager, int playerIdOwner, int bUnknown);
    internal static DetourHandle<c_game_create_new_tribe_delegate> c_game_create_new_tribe_hook = new();
    public static Int64 c_game_create_new_tribe_hook_impl(NativePointer<GameTribeManager> manager, int playerIdOwner, int bUnknown)
    {
        LogHelper.Debug($"pTribeManager={new IntPtr(manager).ToString("X16")}, playerIdOwner={playerIdOwner}, bUnknown={bUnknown}");

        TribeCreateEventArgs eventArgs = new(EventHookPhase.Pre, playerIdOwner, bUnknown);
        TribeR3EventHooks.OnTribeCreate.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_create_new_tribe_hook.Original(
                manager,
                eventArgs.PlayerIdOwner,
                eventArgs.Unknown
            );
            eventArgs.ReturnValue = originalResult;
            TribeCreateEventArgs postEventArgs = new(EventHookPhase.Post, playerIdOwner, bUnknown)
            {
                ReturnValue = originalResult
            };
            TribeR3EventHooks.OnTribeCreate.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }
}
