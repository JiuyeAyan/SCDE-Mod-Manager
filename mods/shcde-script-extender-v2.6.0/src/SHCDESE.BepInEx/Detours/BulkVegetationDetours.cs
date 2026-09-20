using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.Core.Memory.Scanners;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Memory.Scanners;
using Serilog;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Vegetation;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace SHCDESE.Detours;

[SuppressUnmanagedCodeSecurity]
internal unsafe class BulkVegetationDetours
{
    private HookTransaction? tx;
    public BulkVegetationDetours(ReadOnlySpan<byte> memory, ScanRegion region)
    {
        LogHelper.Information($"Applying");

        UInt64 currentImageBase = (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle;
        tx ??= new HookTransaction(region, Plugin.Instance.LoggerFactory);
        DataScanner scanner = DataScanner.Create(region);

        tx.AddDetour(c_game_spawn_vegetation,
            "48 89 6C 24 ?? 56 57 41 56 48 83 EC ?? 83 3D",
             c_game_spawn_vegetation_hook_impl);

        tx.AddDetour(c_game_vegetation_delete,
            "48 89 5C 24 ?? 57 48 83 EC ?? 48 63 FA 48 8B D9 8B D7 48 8D 0D",
             c_game_vegetation_delete_hook_impl);

        tx.AddDetour(c_game_vegetation_tree_take_damage,
            "48 83 EC ?? 48 63 C2 4C 69 D0 ?? ?? ?? ?? 45 39 4C 0A",
             c_game_vegetation_tree_take_damage_hook_impl);

        tx.AddDetour(c_game_vegetation_subtract_resource,
            "48 63 C2 48 69 D0 ?? ?? ?? ?? 44 39 44 0A ?? 75 ?? 0F B7 84 0A",
             c_game_vegetation_subtract_resource_hook_impl);

        tx.AddDetour(c_game_vegetation_tick_handler,
            "40 53 55 56 41 56 41 57",
             c_game_vegetation_tick_handler_hook_impl);

        tx.AddDetour(c_game_vegetation_growth_handler,
            "48 89 74 24 ? 57 48 83 EC ? 83 3D ? ? ? ? ? 48 8B F9",
             c_game_vegetation_growth_handler_hook_impl);

        tx.AddContextHook(c_game_vegetation_growth_hook,
             "FF 05 ?? ?? ?? ?? 8B D6",
             static ctx =>
             {
                UInt64 vegetationAddr = ctx.Pointer->RCX;
                GameVegetation * veg = (GameVegetation*)(vegetationAddr + 0x18);
                UInt64 vegId = ((UInt64)veg - (UInt64)GameVegetationManagerAPI.Instance._vegArray._array) / (UInt64)sizeof(GameVegetation) + 1;
                Log.Debug($"c_game_vegetation_growth_hook: veg={((UInt64)veg).ToString("X")}, vegId={vegId}");

                VegetationGrowthEventArgs eventArgs = new(EventHookPhase.Pre, (Int32)vegId);
                VegetationR3EventHooks.OnVegetationGrowth.Raise(eventArgs);
            }, new RedBird.X64.Hooks.Context.ContextHookOptions() { Registers = RedBird.X64.Assembly.X64SmartCPUContextRegs.Volatile | RedBird.X64.Assembly.X64SmartCPUContextRegs.RBX});

        tx.AddContextHook(c_game_vegetation_tree_fell_hook,
             "48 89 5C 24 ?? 66 45 89 84 0A",
             static ctx =>
             {
                 UInt64 pVegetationManager = ctx.Pointer->RCX;
                UInt64 vegetationId = ctx.Pointer->RDX;
                UInt64 unknown = ctx.Pointer->R8;
                UInt64 vegetationGlobalId = ctx.Pointer->R9;
                Log.Debug($"c_game_vegetation_tree_fell_hook: vegId={vegetationId}, vegGlobId={vegetationGlobalId}");

                VegetationTreeFellEventArgs eventArgs = new(EventHookPhase.Pre, (Int32)vegetationId, (Int32)vegetationGlobalId);
                VegetationR3EventHooks.OnVegetationTreeFell.Raise(eventArgs);
             }, new RedBird.X64.Hooks.Context.ContextHookOptions() { Registers = RedBird.X64.Assembly.X64SmartCPUContextRegs.Volatile});

        DataScanner c_game_create_tree_proximity_area_scan = scanner.Scan(CompiledPattern.Parse("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 48 63 C2 4C 8D 35"));
        if (c_game_create_tree_proximity_area_scan.Found)
        {
            c_game_create_tree_proximity_area = Marshal.GetDelegateForFunctionPointer<c_game_create_tree_proximity_area_delegate>((IntPtr)c_game_create_tree_proximity_area_scan.CurrentAddress);
        }
        else LogHelper.Warning($"Failed to find c_game_create_tree_proximity_area");

        DataScanner c_game_get_tree_proximity_area_level_from_type_scan = scanner.Scan(CompiledPattern.Parse("41 83 F8 ? 7E ? 8D 42"));
        if (c_game_get_tree_proximity_area_level_from_type_scan.Found)
        {
            c_game_get_tree_proximity_area_level_from_type = Marshal.GetDelegateForFunctionPointer<c_game_get_tree_proximity_area_level_from_type_delegate>((IntPtr)c_game_get_tree_proximity_area_level_from_type_scan.CurrentAddress);
        }
        else LogHelper.Warning($"Failed to find c_game_get_tree_proximity_area_level_from_type");

    }

    internal static HookHandle<X64InlineHook> c_game_vegetation_growth_hook = new();
    internal static HookHandle<X64InlineHook> c_game_vegetation_tree_fell_hook = new();

    // 41 83 F8 ? 7E ? 8D 42
    // __int64 __fastcall c_game_get_tree_proximity_area_level_from_type(__int64 pVegetationManager, int vegetationType, int growthStage)
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_get_tree_proximity_area_level_from_type_delegate(NativePointer<GameVegetationManager> manager, VegetationType vegetationType, int growthStage);
    public static c_game_get_tree_proximity_area_level_from_type_delegate c_game_get_tree_proximity_area_level_from_type;

    // 48 89 74 24 ? 57 48 83 EC ? 83 3D ? ? ? ? ? 48 8B F9
    // void __fastcall c_game_vegetation_growth_handler(__int64 pVegManager, unsigned int veg_index, int)
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_vegetation_growth_handler_delegate(NativePointer<GameVegetationManager> manager, int vegetationId, int a3);
    internal static DetourHandle<c_game_vegetation_growth_handler_delegate> c_game_vegetation_growth_handler = new();
    public static Int64 c_game_vegetation_growth_handler_hook_impl(NativePointer<GameVegetationManager> manager, int vegetationId, int a3)
    {
        //LogHelper.Debug($"manager={new IntPtr(manager).ToString("X16")}, vegetationId={vegetationId}, a3={a3}");

        if (!GameVegetationManagerAPI.Instance.VegetationGrowthEnabled)
            return 0;
        return c_game_vegetation_growth_handler.Original(manager, vegetationId, a3);
    }

    // 40 53 55 56 41 56 41 57
    // __int64 __fastcall c_game_vegetation_tick_handler(__int64 pVegManager)
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_vegetation_tick_handler_delegate(NativePointer<GameVegetationManager> manager);
    internal static DetourHandle<c_game_vegetation_tick_handler_delegate> c_game_vegetation_tick_handler = new();
    public static Int64 c_game_vegetation_tick_handler_hook_impl(NativePointer<GameVegetationManager> manager)
    {
        //LogHelper.Debug($"manager={new IntPtr(manager).ToString("X16")}");

        if (!GameVegetationManagerAPI.Instance.VegetationSimulationEnabled)
            return 0;
        return c_game_vegetation_tick_handler.Original(manager);
    }

    // __int64 __fastcall c_game_create_tree_proximity_area(__int64 pTileManager, int vegetationId, int bUnknown)
    // 48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 48 63 C2 4C 8D 35
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void c_game_create_tree_proximity_area_delegate(IntPtr pTileManager, int vegetationId, int bUnknown);

    public static c_game_create_tree_proximity_area_delegate c_game_create_tree_proximity_area;

    // __int64 __fastcall c_game_vegetation_subtract_resource(__int64 pVegetationManager, int vegetationId, int vegetationGlobalId)
    // 48 63 C2 48 69 D0 ? ? ? ? 44 39 44 0A ? 75 ? 0F B7 84 0A
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_vegetation_subtract_resource_delegate(NativePointer<GameVegetationManager> manager, int vegetationId, int vegetationGlobalId);
    internal static DetourHandle<c_game_vegetation_subtract_resource_delegate> c_game_vegetation_subtract_resource = new();
    public static Int64 c_game_vegetation_subtract_resource_hook_impl(NativePointer<GameVegetationManager> manager, int vegetationId, int vegetationGlobalId)
    {
        LogHelper.Debug($"manager={new IntPtr(manager).ToString("X16")}, vegetationId={vegetationId}, vegGlobalId={vegetationGlobalId}");
        
        VegetationSubtractResourceEventArgs eventArgs = new(EventHookPhase.Pre, vegetationId, vegetationGlobalId);
        VegetationR3EventHooks.OnVegetationSubtractResource.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_vegetation_subtract_resource.Original(
                manager,
                eventArgs.VegetationId,
                eventArgs.VegetationGlobalId
            );
            eventArgs.ReturnValue = originalResult;
            VegetationSubtractResourceEventArgs postEventArgs = new(EventHookPhase.Post, vegetationId, vegetationGlobalId)
            {
                ReturnValue = originalResult
            };
            VegetationR3EventHooks.OnVegetationSubtractResource.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    // __int64 __fastcall c_game_vegetation_tree_take_damage(__int64 pVegetationManager, int vegetationId, __int16 damage, int vegetationGlobalId)
    // 48 83 EC ?? 48 63 C2 4C 69 D0 ?? ?? ?? ?? 45 39 4C 0A
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_vegetation_tree_take_damage_delegate(NativePointer<GameVegetationManager> manager, int vegetationId, int damage, int vegetationGlobalId);
    internal static DetourHandle<c_game_vegetation_tree_take_damage_delegate> c_game_vegetation_tree_take_damage = new();
    public static Int64 c_game_vegetation_tree_take_damage_hook_impl(NativePointer<GameVegetationManager> manager, int vegetationId, int damage, int vegetationGlobalId)
    {
        LogHelper.Debug($"manager={new IntPtr(manager).ToString("X16")}, vegetationId={vegetationId}, damage={damage} vegGlobalId={vegetationGlobalId}");
        
        VegetationTreeDamagedEventArgs eventArgs = new(EventHookPhase.Pre, vegetationId, damage, vegetationGlobalId);
        VegetationR3EventHooks.OnVegetationTreeDamaged.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_vegetation_tree_take_damage.Original(
                manager,
                eventArgs.VegetationId,
                eventArgs.Damage,
                eventArgs.VegetationGlobalId
            );
            eventArgs.ReturnValue = originalResult;
            VegetationTreeDamagedEventArgs postEventArgs = new(EventHookPhase.Post, vegetationId, damage, vegetationGlobalId)
            {
                ReturnValue = originalResult
            };
            VegetationR3EventHooks.OnVegetationTreeDamaged.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    // void *__fastcall c_game_vegetation_delete(__int64 pVegetationManager, unsigned int vegId)
    // 48 89 5C 24 ? 57 48 83 EC ? 48 63 FA 48 8B D9 8B D7 48 8D 0D
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_vegetation_delete_delegate(NativePointer<GameVegetationManager> manager, int vegetationId);
    internal static DetourHandle<c_game_vegetation_delete_delegate> c_game_vegetation_delete = new();
    public static Int64 c_game_vegetation_delete_hook_impl(NativePointer<GameVegetationManager> manager, int vegetationId)
    {
        LogHelper.Debug($"manager={new IntPtr(manager).ToString("X16")}, vegetationId={vegetationId}");

        VegetationDeleteEventArgs eventArgs = new(EventHookPhase.Pre, vegetationId);
        VegetationR3EventHooks.OnVegetationDelete.Raise(eventArgs);
 
        Int64 originalResult = c_game_vegetation_delete.Original(
            manager,
            eventArgs.VegetationId
        );
        VegetationDeleteEventArgs postEventArgs = new(EventHookPhase.Post, vegetationId);
        VegetationR3EventHooks.OnVegetationDelete.Raise(postEventArgs);

        return originalResult;
    }

    // __int64 __fastcall c_game_spawn_vegetation(_DWORD *pVegManager, __int16 tile_x, __int16 tile_y, int vegetation_type, __int16 a5, int a6, __int16 a7, int growthStage)
    // 48 89 6C 24 ? 56 57 41 56 48 83 EC ? 83 3D
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_spawn_vegetation_delegate(NativePointer<GameVegetationManager> manager, UInt16 tileX, UInt16 tileY, VegetationType vegType, Int16 a5, int a6, Int16 a7, int growthStage);
    internal static DetourHandle<c_game_spawn_vegetation_delegate> c_game_spawn_vegetation = new();
    public static Int64 c_game_spawn_vegetation_hook_impl(NativePointer<GameVegetationManager> manager, UInt16 tileX, UInt16 tileY, VegetationType vegType, Int16 a5, int a6, Int16 a7, int growthStage)
    {
        LogHelper.Debug($"manager={new IntPtr(manager).ToString("X16")}, tileX={tileX}, tileY={tileY}, vegType={vegType}, a5={a5}, a6={a6}, a7={a7}, growthStage={growthStage}");

        VegetationCreateEventArgs eventArgs = new(EventHookPhase.Pre, tileX, tileY, vegType, a5, a6, a7, growthStage);
        VegetationR3EventHooks.OnVegetationCreate.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_spawn_vegetation.Original(
                manager,
                eventArgs.TileX,
                eventArgs.TileY,
                eventArgs.VegetationType,
                eventArgs.Unknown1,
                eventArgs.Unknown2,
                eventArgs.Unknown3,
                eventArgs.GrowthStage
            );
            eventArgs.ReturnValue = originalResult;
            VegetationCreateEventArgs postEventArgs = new(EventHookPhase.Post, tileX, tileY, vegType, a5, a6, a7, growthStage)
            {
                ReturnValue = originalResult
            };
            VegetationR3EventHooks.OnVegetationCreate.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }
}
