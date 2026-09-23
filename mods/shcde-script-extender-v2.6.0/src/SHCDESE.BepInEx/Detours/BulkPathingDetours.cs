using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.Core.Memory.Scanners;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace SHCDESE.Detours;

[SuppressUnmanagedCodeSecurity]
public class BulkPathingDetours
{
    public BulkPathingDetours(ReadOnlySpan<byte> memory, ScanRegion region, HookTransaction tx, DataScanner scanner)
    {
        LogHelper.Information($"Applying");

        UInt64 currentImageBase = (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle;

        DataScanner c_game_pathfinding_find_next_component_toward_destinaton_scan = scanner.Scan(CompiledPattern.Parse("40 55 41 54 41 55 41 56 48 8D AC 24"));
        if (c_game_pathfinding_find_next_component_toward_destinaton_scan.Found)
        {
            c_game_pathfinding_find_next_component_toward_destinaton = Marshal.GetDelegateForFunctionPointer<c_game_pathfinding_find_next_component_toward_destinaton_delegate>((IntPtr)c_game_pathfinding_find_next_component_toward_destinaton_scan.CurrentAddress);
        }
        else LogHelper.Warning($"Failed to find c_game_queue_chore");

        tx.AddDetour(c_game_unit_validate_next_tile_surface_for_type_hook,
            "48 63 C2 4C 8D 1D ? ? ? ? 4C 69 D0 ? ? ? ? 43 0F BF 84 1A",
            c_game_unit_validate_next_tile_surface_for_type_hook_impl);

        tx.AddDetour(c_game_unit_selection_contains_only_assassins_hook,
            HookTarget.FromRelativeCall("E8 ? ? ? ? 85 C0 75 ? 48 8B F3"),
            c_game_unit_selection_contains_only_assassins_hook_impl);

    }

    // __int64 __fastcall c_game_pathfinding_rebuild(__int64 pPathfindingContext, int forceImmediate)
    // 40 53 41 57 48 83 EC ? 48 8B D9

    // Returns the first PCL to enter when routing from currentPclId toward destinationPclId
    // __int64 __fastcall c_game_pathfinding_find_next_component_toward_destinaton(int *pPathfindingContext, int playerId, unsigned int currentPclId, unsigned int destinationPclId, int connectionClassMode)
    // 40 55 41 54 41 55 41 56 48 8D AC 24
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_pathfinding_find_next_component_toward_destinaton_delegate(IntPtr pPathfindingContext, int playerId, int currentPclId, int destinationPclId, PathConnectionQueryMode connectionClassMode);
    internal static c_game_pathfinding_find_next_component_toward_destinaton_delegate c_game_pathfinding_find_next_component_toward_destinaton;

    // __int64 __fastcall c_game_unit_validate_next_tile_surface_for_type(__int64 pPathfindingContext, int unitId, int currentTileId, int currentTileY, int direction)
    // 48 63 C2 4C 8D 1D ? ? ? ? 4C 69 D0 ? ? ? ? 43 0F BF 84 1A
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_unit_validate_next_tile_surface_for_type_delegate(IntPtr pPathfindingContext, int unitId, int currentTileId, int currentTileY, int direction);
    internal static DetourHandle<c_game_unit_validate_next_tile_surface_for_type_delegate> c_game_unit_validate_next_tile_surface_for_type_hook = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int64 c_game_unit_validate_next_tile_surface_for_type_hook_impl(IntPtr pPathfindingContext, int unitId, int currentTileId, int currentTileY, int direction)
    {
        if (GamePathingManagerAPI.Instance.ShouldBypassNextTileSurfaceValidation(unitId))
            return 1;

        return c_game_unit_validate_next_tile_surface_for_type_hook.Original(pPathfindingContext, unitId, currentTileId, currentTileY, direction);
    }

    // __int64 __fastcall c_game_unit_selection_contains_only_assassins(__int64 pUnitManager)
    // E8 ? ? ? ? 85 C0 75 ? 48 8B F3
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_unit_selection_contains_only_assassins_delegate(IntPtr pUnitManager);
    internal static DetourHandle<c_game_unit_selection_contains_only_assassins_delegate> c_game_unit_selection_contains_only_assassins_hook = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe static Int64 c_game_unit_selection_contains_only_assassins_hook_impl(IntPtr pUnitManager)
    {
        Int64 originalResult = c_game_unit_selection_contains_only_assassins_hook.Original(pUnitManager);
        if (originalResult != 0)
            return originalResult;

        return GamePathingManagerAPI.Instance.DoesSelectionContainOnlyAssassinsOrOverrides((GameUnitManager*)pUnitManager) ? 1 : 0;
    }
}
