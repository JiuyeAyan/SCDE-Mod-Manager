using RedBird.Core.Memory;
using RedBird.Core.Memory.Scanners;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API.LowLevel;
using SHCDESE.Logging;
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace SHCDESE.Detours;

[SuppressUnmanagedCodeSecurity]
public class BulkTileDetours
{
    public BulkTileDetours(ReadOnlySpan<byte> memory, ScanRegion region)
    {
        LogHelper.Information($"Applying");
        UInt64 currentImageBase = (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle;
        DataScanner scanner = DataScanner.Create(region);

        DataScanner c_game_update_visual_resourcetile_scan = scanner.Scan(CompiledPattern.Parse("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 4C 8D 05"));
        if (c_game_update_visual_resourcetile_scan.Found)
        {
            c_game_update_visual_resourcetile = Marshal.GetDelegateForFunctionPointer<c_game_update_visual_resourcetile_delegate>((IntPtr)c_game_update_visual_resourcetile_scan.CurrentAddress);
        }
        else LogHelper.Warning($"Failed to find c_game_update_visual_goodsyard_goods");

        DataScanner c_game_update_pathfinding_for_tile_and_neighbors3x3_scan = scanner.Scan(CompiledPattern.Parse("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC ?? 4C 63 F2 45 8B F8"));
        if (c_game_update_pathfinding_for_tile_and_neighbors3x3_scan.Found)
        {
            c_game_update_pathfinding_for_tile_and_neighbors3x3 = Marshal.GetDelegateForFunctionPointer<c_game_update_pathfinding_for_tile_and_neighbors3x3_delegate>((IntPtr)c_game_update_pathfinding_for_tile_and_neighbors3x3_scan.CurrentAddress);
        }
        else LogHelper.Warning($"Failed to find c_game_update_pathfinding_for_tile_and_neighbors3x3");

        DataScanner c_game_update_pathfinding_for_tile_and_neighbors3x3_wrapper_scan = scanner.Scan(CompiledPattern.Parse("40 53 48 83 EC ? 48 8B D9 E8 ? ? ? ? 83 BB"));
        if (c_game_update_pathfinding_for_tile_and_neighbors3x3_wrapper_scan.Found)
        {
            c_game_update_pathfinding_for_tile_and_neighbors3x3_wrapper = Marshal.GetDelegateForFunctionPointer<c_game_update_pathfinding_for_tile_and_neighbors3x3_wrapper_delegate>((IntPtr)c_game_update_pathfinding_for_tile_and_neighbors3x3_wrapper_scan.CurrentAddress);
        }
        else LogHelper.Warning($"Failed to find c_game_update_pathfinding_for_tile_and_neighbors3x3");

    }

    //
    // char __fastcall c_game_update_visual_resourcetile(unsigned int *pTileManager, int goodsyardId)
    // 48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 4C 8D 05
    // Call-only
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate byte c_game_update_visual_resourcetile_delegate(IntPtr pTileManager, int buildingId);
    public static c_game_update_visual_resourcetile_delegate c_game_update_visual_resourcetile;

    // __int64 __fastcall c_game_update_pathfinding_pot(__int64 p_gPathfindingContext, int centerTileY, __int64 centerTileId)
    // 40 53 48 83 EC ? 48 8B D9 E8 ? ? ? ? 83 BB
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void c_game_update_pathfinding_for_tile_and_neighbors3x3_wrapper_delegate(IntPtr pPathfindingContext, int centerTileY, int centerTileId);
    public static c_game_update_pathfinding_for_tile_and_neighbors3x3_wrapper_delegate c_game_update_pathfinding_for_tile_and_neighbors3x3_wrapper;

    // __int64 __fastcall c_game_update_pathfinding_for_tile_and_neighbors3x3(__int64 pPathfindingCtx, int centerTileY, __int64 centerTileId)
    // 48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC ?? 4C 63 F2 45 8B F8
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void c_game_update_pathfinding_for_tile_and_neighbors3x3_delegate(IntPtr pPathfindingContext, int centerTileY, int centerTileId);
    public static c_game_update_pathfinding_for_tile_and_neighbors3x3_delegate c_game_update_pathfinding_for_tile_and_neighbors3x3;
}
