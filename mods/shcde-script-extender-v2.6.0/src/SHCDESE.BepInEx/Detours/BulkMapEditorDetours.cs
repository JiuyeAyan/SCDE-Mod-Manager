using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.DebugMenu;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapEditor;
using SHCDESE.ImMenu.MapEditor;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace SHCDESE.Detours;

/// <summary>
/// Manages the application of all native detours (hooks) related to the game's map editor functions.
/// </summary>
/// <remarks>
/// This static class is responsible for intercepting core map editor actions, such as painting terrain,
/// adjusting height, and placing units. By hooking these functions, it provides an entry point for custom
/// tools like the <see cref="MirrorTool"/> to inject their logic, allowing actions to be mirrored across the map.
/// </remarks>
[SuppressUnmanagedCodeSecurity]
internal unsafe class BulkMapEditorDetours
{
    /// <summary>
    /// This is for an edge-case where the game uses the SetTileTypeToNone function to "clean" surface before redrawing it with another one.
    /// </summary>
    private static bool _suppressMirrorForSetTileTypeToNone = false;

    /// <summary>
    /// Scans the game's memory for all map editor-related function signatures and applies the corresponding detours.
    /// </summary>
    /// <param name="memory">A <see cref="ReadOnlySpan{T}"/> of bytes representing the memory block of the loaded `CrusaderDE.dll`.</param>
    /// <param name="region">The memory region to scan for function signatures.</param>
    /// <param name="tx">Shared transaction.</param>
    /// <param name="scanner">Shared scanner.</param>
    /// <remarks>
    /// This method should only be called once during the script extender's initialization phase. It finds the
    /// target functions using AOB (Array of Bytes) scanning and redirects them to our custom hook implementations.
    /// </remarks>
    public BulkMapEditorDetours(ReadOnlySpan<byte> memory, ScanRegion region, HookTransaction tx, DataScanner scanner)
    {
        LogHelper.Information($"Applying");

        UInt64 currentImageBase = (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle;

        tx.AddDetour(c_game_editor_brush_set_tiletype_hook,
            "48 89 5C 24 ? 44 89 4C 24 ? 44 89 44 24 ? 89 54 24 ? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ? 4D 63 E1",
            c_game_editor_brush_set_tiletype_hook_impl);

        tx.AddDetour(c_game_editor_brush_set_tiletype_to_none_hook,
            "4C 8B DC 45 89 43 ? 89 54 24 ? 48 83 EC ? 49 89 73",
            c_game_editor_brush_set_tiletype_to_none_hook_impl);

        tx.AddDetour(c_game_editor_brush_set_height_to_preset_hook,
            "4C 8B DC 45 89 43 ? 89 54 24 ? 48 83 EC ? 49 89 5B",
            c_game_editor_brush_set_height_to_preset_hook_impl);

        tx.AddDetour(c_game_editor_brush_set_height_to_number_hook,
             "4C 8B DC 45 89 43 ? 89 54 24 ? 57 48 81 EC ? ? ? ? 49 89 5B",
             c_game_editor_brush_set_height_to_number_hook_impl);

        tx.AddDetour(c_game_editor_brush_even_height_hook,
            "4C 8B DC 45 89 43 ? 89 54 24 ? 48 81 EC",
            c_game_editor_brush_even_height_hook_impl);

        tx.AddDetour(c_game_editor_brush_raise_tileheight_hook,
            "4C 8B DC 45 89 43 ? 89 54 24 ? 57 48 81 EC ? ? ? ? 4D 89 63",
            c_game_editor_brush_raise_tileheight_hook_impl);

        tx.AddDetour(c_game_editor_spawn_unit_hook,
            "48 89 5C 24 ?? 48 89 6C 24 ?? 56 57 41 54 41 55 41 56 48 83 EC ?? 48 63 9C 24",
            c_game_editor_spawn_unit_hook_impl);

        tx.AddDetour(c_game_editor_place_vegetation_hook,
            "48 8B C4 56",
            c_game_editor_place_vegetation_hook_impl);

        tx.AddDetour(c_game_editor_place_animal_hook,
            "44 89 4C 24 ?? 44 89 44 24 ?? 89 54 24 ?? 53 55 56 57 41 54 41 55 41 57",
            c_game_editor_place_animal_hook_impl);

        tx.AddDetour(c_game_editor_brush_delete_hook,
            "4C 8B DC 45 89 43 ? 53 41 55",
            c_game_editor_brush_delete_hook_impl);

        if (scanner.Scan("E8 ? ? ? ? 4C 8D 05 ? ? ? ? 81 A4 B3").TryReadFlowControlTarget(out UInt64 c_game_tile_refresh_visual_rva))
        {
            c_game_tile_refresh_visual = Marshal.GetDelegateForFunctionPointer<c_game_tile_refresh_visual_delegate>((IntPtr)(c_game_tile_refresh_visual_rva));
            LogHelper.Information($"c_game_tile_refresh_visual found at {(c_game_tile_refresh_visual_rva).ToString("X16")}");
        }
        else LogHelper.Error($"Could not retrieve function ptr to c_game_tile_refresh_visual");

    }

    // 44 89 4C 24 ? 44 89 44 24 ? 89 54 24 ? 53 55 56 57 41 54 41 55 41 57
    // __int64 __fastcall c_game_editor_place_animal(int *pTribeManager, int eMappersValue, int tile_y_raw_arg, int tile_x_raw_arg, __int16 heightElevation)

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_editor_place_animal_delegate(NativePointer<GameTribeManager> manager, eMappers eMappers, int worldTileX, int worldTileY, UInt16 heightElevation);
    internal static DetourHandle<c_game_editor_place_animal_delegate> c_game_editor_place_animal_hook = new();
    public static Int64 c_game_editor_place_animal_hook_impl(NativePointer<GameTribeManager> manager, eMappers eMappers, int worldTileX, int worldTileY, UInt16 heightElevation)
    {
        LogHelper.Debug($"manager={new IntPtr(manager).ToString("X16")}, eMappers={eMappers}, worldTileX={worldTileX}, worldTileY={worldTileY}, heightElevation={heightElevation}");

        if (DebugMenuManager.Instance._mirrorTool.AnimalMirrorEnabled)
        {
            GameTileManagerAPI gtm = GameTileManagerAPI.Instance;
            int centerTileId = gtm.GetTileId(worldTileX, worldTileY);
            MirrorTool.ExecuteMirrorAction(centerTileId, worldTileY, (mirroredId, mirroredX, mirroredY) =>
            {
                c_game_editor_place_animal_hook.Original(
                    manager,
                    eMappers,
                    mirroredX,
                    mirroredY,
                    heightElevation);
            });
        }

        AnimalPlaceEventArgs eventArgs = new(EventHookPhase.Pre, eMappers, worldTileX, worldTileY, heightElevation);
        MapEditorR3EventHooks.OnAnimalPlace.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_editor_place_animal_hook.Original(
                manager,
                eventArgs.MappersValue,
                eventArgs.WorldTileX,
                eventArgs.WorldTileY,
                eventArgs.HeightElevation
            );
            eventArgs.ReturnValue = originalResult;
            AnimalPlaceEventArgs postEventArgs = new(EventHookPhase.Post, eMappers, worldTileX, worldTileY, heightElevation)
            {
                ReturnValue = originalResult
            };
            MapEditorR3EventHooks.OnAnimalPlace.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    // We use this function to update the tile area after placement (Without this the changes will only be seen upon forcefully trigging a global redraw
    // like flattening the landscape or re-opening the map, etc.
    internal delegate void c_game_tile_refresh_visual_delegate(IntPtr p_pPathfindingContext, int a2, int tileX, int tileY);
    internal static c_game_tile_refresh_visual_delegate c_game_tile_refresh_visual;

    // __int64 __fastcall c_game_editor_place_vegetation(__int64 pTileManager, unsigned int tile_x, int tile_y, eMappers eMappers)
    // 48 8B C4 56
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_editor_place_vegetation_delegate(IntPtr pTileManager, int tileX, int tileY, eMappers eMappers);
    internal static DetourHandle<c_game_editor_place_vegetation_delegate> c_game_editor_place_vegetation_hook = new();
    public unsafe static Int64 c_game_editor_place_vegetation_hook_impl(IntPtr pTileManager, int tileX, int tileY, eMappers eMappers)
    {
        LogHelper.Debug($"manager={pTileManager.ToString("X16")}, tileX={tileX}, tileY={tileY}, eMappers={eMappers}");

        if (DebugMenuManager.Instance._mirrorTool.VegetationMirrorEnabled)
        {
            GameTileManagerAPI gtm = GameTileManagerAPI.Instance;
            int centerTileId = gtm.GetTileId(tileX, tileY);
            MirrorTool.ExecuteMirrorAction(centerTileId, tileY, (mirroredId, mirroredX, mirroredY) =>
            {
                c_game_editor_place_vegetation_hook.Original!(pTileManager, mirroredX, mirroredY, eMappers);
            });
        }

        VegetationPlaceEventArgs eventArgs = new(EventHookPhase.Pre, tileX, tileY, eMappers);
        MapEditorR3EventHooks.OnVegetationPlace.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_editor_place_vegetation_hook.Original!(
                pTileManager,
                eventArgs.TileX,
                eventArgs.TileY,
                eventArgs.Mappers
            );
            eventArgs.ReturnValue = originalResult;
            VegetationPlaceEventArgs postEventArgs = new(EventHookPhase.Post, tileX, tileY, eMappers)
            {
                ReturnValue = originalResult
            };
            MapEditorR3EventHooks.OnVegetationPlace.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    // __int64 __fastcall c_game_editor_brush_set_tiletype(__int64 pTileManager, int brushSize, int centerTileId, int centerTileY, int a5, int a6, int tileType, char a8)
    // 48 89 5C 24 ?? 44 89 4C 24 ?? 44 89 44 24 ?? 89 54 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 4D 63 E1
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_editor_brush_set_tiletype_delegate(IntPtr pTileManager, int brushSize, int centerTileId, int centerTileY, int a5, int tileProperty, int tileType, byte a8);
    internal static DetourHandle<c_game_editor_brush_set_tiletype_delegate> c_game_editor_brush_set_tiletype_hook = new();
    public unsafe static Int64 c_game_editor_brush_set_tiletype_hook_impl(IntPtr pTileManager, int brushSize, int centerTileId, int centerTileY, int a5, int tileProperty, int tileType, byte a8)
    {
        LogHelper.Debug($"brushSize={brushSize}, centerTileId={centerTileId.ToString("X8")}, centerTileY={centerTileY}, a5={a5}, tileProperty={(TilePropertyFlag)tileProperty}, tileType={(TileType)tileType}, a8={a8}");

        if (DebugMenuManager.Instance._tilePropertyMutatorTool.Enabled)
        {
            tileProperty = DebugMenuManager.Instance._tilePropertyMutatorTool.ExecuteAugmentAction(tileProperty);
        }

        _suppressMirrorForSetTileTypeToNone = true;

        if (DebugMenuManager.Instance._mirrorTool.TileTypeMirrorEnabled)
        {
            MirrorTool.ExecuteMirrorAction(centerTileId, centerTileY, (mirroredId, mirroredX, mirroredY) =>
            {
                c_game_editor_brush_set_tiletype_hook.Original!(pTileManager, brushSize, mirroredId, mirroredY, a5, tileProperty, tileType, a8);
                c_game_tile_refresh_visual(GameTileManagerAPI.Instance.GetPathfindingContext(), 9, mirroredX, mirroredY);
            });
        }

        BrushSetTileTypeEventArgs eventArgs = new(EventHookPhase.Pre, brushSize, centerTileId, centerTileY, a5, (TilePropertyFlag)tileProperty, (TileType)tileType, a8 == 1);
        MapEditorR3EventHooks.OnSetTileType.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_editor_brush_set_tiletype_hook.Original!(
                pTileManager,
                eventArgs.BrushSize,
                eventArgs.CenterTileId,
                eventArgs.CenterTileY,
                eventArgs.Unknown1,
                (int)eventArgs.TileProperty,
                (int)eventArgs.TileType,
                eventArgs.Unknown2 ? (byte)1 : (byte)0
            );
            eventArgs.ReturnValue = originalResult;
            BrushSetTileTypeEventArgs postEventArgs = new(EventHookPhase.Post, brushSize, centerTileId, centerTileY, a5, (TilePropertyFlag)tileProperty, (TileType)tileType, a8 == 1)
            {
                ReturnValue = originalResult
            };
            MapEditorR3EventHooks.OnSetTileType.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }

        _suppressMirrorForSetTileTypeToNone = false;

        return eventArgs.ReturnValue;
    }

    // __int64 __fastcall c_game_editor_brush_set_tiletype_to_none(__int64 pTileManager, int centerTileId, int centerTileY, int brushSize)
    // 4C 8B DC 45 89 43 ? 89 54 24 ? 48 83 EC ? 49 89 73
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_editor_brush_set_tiletype_to_none_delegate(IntPtr pTileManager, int centerTileId, int centerTileY, int brushSize);
    internal static DetourHandle<c_game_editor_brush_set_tiletype_to_none_delegate> c_game_editor_brush_set_tiletype_to_none_hook = new();
    public unsafe static Int64 c_game_editor_brush_set_tiletype_to_none_hook_impl(IntPtr pTileManager, int centerTileId, int centerTileY, int brushSize)
    {
        LogHelper.Debug($"centerTileId={centerTileId.ToString("X8")}, centerTileY={centerTileY}, brushSize={brushSize}");

        if (DebugMenuManager.Instance._mirrorTool.TileTypeMirrorEnabled && !_suppressMirrorForSetTileTypeToNone)
        {
            MirrorTool.ExecuteMirrorAction(centerTileId, centerTileY, (mirroredId, mirroredX, mirroredY) =>
            {
                c_game_editor_brush_set_tiletype_to_none_hook.Original!(pTileManager, mirroredId, mirroredY, brushSize);
                c_game_tile_refresh_visual(GameTileManagerAPI.Instance.GetPathfindingContext(), 9, mirroredX, mirroredY);
            });
        }

        BrushSetTileTypeToNoneEventArgs eventArgs = new(EventHookPhase.Pre, centerTileId, centerTileY, brushSize);
        MapEditorR3EventHooks.OnSetTileTypeToNone.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_editor_brush_set_tiletype_to_none_hook.Original!(
                pTileManager,
                eventArgs.CenterTileId,
                eventArgs.CenterTileY,
                eventArgs.BrushSize
            );
            eventArgs.ReturnValue = originalResult;
            BrushSetTileTypeToNoneEventArgs postEventArgs = new(EventHookPhase.Post, centerTileId, centerTileY, brushSize)
            {
                ReturnValue = originalResult
            };
            MapEditorR3EventHooks.OnSetTileTypeToNone.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    // __int64 __fastcall c_game_editor_brush_set_height(__int64 pTileManager, int centerTileId, int centerTileY, int brushSize, int heightPreset)
    // 44 89 44 24 ?? 89 54 24 ?? 53 56 57 41 54 41 55 41 56 48 83 EC ?? 33 F6
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_editor_brush_set_height_to_preset_delegate(IntPtr pTileManager, int centerTileId, int centerTileY, int brushSize, int heightPreset);
    internal static DetourHandle<c_game_editor_brush_set_height_to_preset_delegate> c_game_editor_brush_set_height_to_preset_hook = new();
    public unsafe static Int64 c_game_editor_brush_set_height_to_preset_hook_impl(IntPtr pTileManager, int centerTileId, int centerTileY, int brushSize, int heightPreset)
    {
        LogHelper.Debug($"centerTileId={centerTileId.ToString("X16")}, centerTileY={centerTileY}, brushSize={brushSize}, heightPreset={heightPreset}");

        if (DebugMenuManager.Instance._mirrorTool.TileHeightMirrorEnabled)
        {
            MirrorTool.ExecuteMirrorAction(centerTileId, centerTileY, (mirroredId, mirroredX, mirroredY) =>
            {
                c_game_editor_brush_set_height_to_preset_hook.Original!(pTileManager, mirroredId, mirroredY, brushSize, heightPreset);
                c_game_tile_refresh_visual(GameTileManagerAPI.Instance.GetPathfindingContext(), 9, mirroredX, mirroredY);
            });
        }

        BrushSetTileHeightToPresetEventArgs eventArgs = new(EventHookPhase.Pre, centerTileId, centerTileY, brushSize, heightPreset);
        MapEditorR3EventHooks.OnSetTileHeightToPreset.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_editor_brush_set_height_to_preset_hook.Original!(
                pTileManager,
                eventArgs.CenterTileId,
                eventArgs.CenterTileY,
                eventArgs.BrushSize,
                eventArgs.HeightPreset
            );
            eventArgs.ReturnValue = originalResult;
            BrushSetTileHeightToPresetEventArgs postEventArgs = new(EventHookPhase.Post, centerTileId, centerTileY, brushSize, heightPreset)
            {
                ReturnValue = originalResult
            };
            MapEditorR3EventHooks.OnSetTileHeightToPreset.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }


    // __int64 __fastcall c_game_editor_brush_raise_tileheight(_DWORD *pTileManager, int centerTileId, int centerTileY, int brushSize, int a5)
    // 4C 8B DC 45 89 43 ? 89 54 24 ? 57 48 81 EC ? ? ? ? 4D 89 63
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_editor_brush_raise_tileheight_delegate(IntPtr pTileManager, int centerTileId, int centerTileY, int brushSize, int raiseModifier);
    internal static DetourHandle<c_game_editor_brush_raise_tileheight_delegate> c_game_editor_brush_raise_tileheight_hook = new();
    public unsafe static Int64 c_game_editor_brush_raise_tileheight_hook_impl(IntPtr pTileManager, int centerTileId, int centerTileY, int brushSize, int raiseModifier)
    {
        LogHelper.Debug($"centerTileId={centerTileId.ToString("X8")}, centerTileY={centerTileY}, brushSize={brushSize}, raiseModifier={raiseModifier}");

        if (DebugMenuManager.Instance._mirrorTool.TileHeightMirrorEnabled)
        {
            MirrorTool.ExecuteMirrorAction(centerTileId, centerTileY, (mirroredId, mirroredX, mirroredY) =>
            {
                c_game_editor_brush_raise_tileheight_hook.Original!(pTileManager, mirroredId, mirroredY, brushSize, raiseModifier);
                c_game_tile_refresh_visual(GameTileManagerAPI.Instance.GetPathfindingContext(), 9, mirroredX, mirroredY);
            });
        }

        BrushRaiseTileHeightEventArgs eventArgs = new(EventHookPhase.Pre, centerTileId, centerTileY, brushSize, raiseModifier);
        MapEditorR3EventHooks.OnRaiseTileHeight.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_editor_brush_raise_tileheight_hook.Original!(
                pTileManager,
                eventArgs.CenterTileId,
                eventArgs.CenterTileY,
                eventArgs.BrushSize,
                eventArgs.RaiseModifier
            );
            eventArgs.ReturnValue = originalResult;
            BrushRaiseTileHeightEventArgs postEventArgs = new(EventHookPhase.Post, centerTileId, centerTileY, brushSize, raiseModifier)
            {
                ReturnValue = originalResult
            };
            MapEditorR3EventHooks.OnRaiseTileHeight.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    // __int64 __fastcall c_game_editor_brush_set_height_to_number(_DWORD *pTileManager, int centerTileId, int centerTileY, int brushSize, __int16 height)
    // 4C 8B DC 45 89 43 ? 89 54 24 ? 57 48 81 EC ? ? ? ? 49 89 5B
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_editor_brush_set_height_to_number_delegate(IntPtr pTileManager, int centerTileId, int centerTileY, int brushSize, Int16 height);
    internal static DetourHandle<c_game_editor_brush_set_height_to_number_delegate> c_game_editor_brush_set_height_to_number_hook = new();
    public unsafe static Int64 c_game_editor_brush_set_height_to_number_hook_impl(IntPtr pTileManager, int centerTileId, int centerTileY, int brushSize, Int16 height)
    {
        LogHelper.Debug($"centerTileId={centerTileId.ToString("X8")}, centerTileY={centerTileY}, brushSize={brushSize}, height={height}");

        if (DebugMenuManager.Instance._mirrorTool.TileHeightMirrorEnabled)
        {
            MirrorTool.ExecuteMirrorAction(centerTileId, centerTileY, (mirroredId, mirroredX, mirroredY) =>
            {
                c_game_editor_brush_set_height_to_number_hook.Original!(pTileManager, mirroredId, mirroredY, brushSize, height);
                c_game_tile_refresh_visual(GameTileManagerAPI.Instance.GetPathfindingContext(), 9, mirroredX, mirroredY);
            });
        }

        BrushSetTileHeightToNumberEventArgs eventArgs = new(EventHookPhase.Pre, centerTileId, centerTileY, brushSize, height);
        MapEditorR3EventHooks.OnSetTileHeightToNumber.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_editor_brush_set_height_to_number_hook.Original!(
                pTileManager,
                eventArgs.CenterTileId,
                eventArgs.CenterTileY,
                eventArgs.BrushSize,
                eventArgs.Height
            );
            eventArgs.ReturnValue = originalResult;
            BrushSetTileHeightToNumberEventArgs postEventArgs = new(EventHookPhase.Post, centerTileId, centerTileY, brushSize, height)
            {
                ReturnValue = originalResult
            };
            MapEditorR3EventHooks.OnSetTileHeightToNumber.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    // __int64 __fastcall c_game_editor_brush_even_height(_DWORD *pTileManager, int centerTileId, int centerTileY, int brushSize)
    // 4C 8B DC 45 89 43 ? 89 54 24 ? 48 81 EC
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_editor_brush_even_height_delegate(IntPtr pTileManager, int centerTileId, int centerTileY, int brushSize);
    internal static DetourHandle<c_game_editor_brush_even_height_delegate> c_game_editor_brush_even_height_hook = new();
    public unsafe static Int64 c_game_editor_brush_even_height_hook_impl(IntPtr pTileManager, int centerTileId, int centerTileY, int brushSize)
    {
        LogHelper.Information($"centerTileId={centerTileId.ToString("X8")}, centerTileY={centerTileY}, brushSize={brushSize}");

        if (DebugMenuManager.Instance._mirrorTool.TileHeightMirrorEnabled)
        {
            MirrorTool.ExecuteMirrorAction(centerTileId, centerTileY, (mirroredId, mirroredX, mirroredY) =>
            {
                c_game_editor_brush_even_height_hook.Original!(pTileManager, mirroredId, mirroredY, brushSize);
                c_game_tile_refresh_visual(GameTileManagerAPI.Instance.GetPathfindingContext(), 9, mirroredX, mirroredY);
            });
        }

        BrushEvenTileHeightEventArgs eventArgs = new(EventHookPhase.Pre, centerTileId, centerTileY, brushSize);
        MapEditorR3EventHooks.OnEvenTileHeight.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_editor_brush_even_height_hook.Original!(
                pTileManager,
                eventArgs.CenterTileId,
                eventArgs.CenterTileY,
                eventArgs.BrushSize
            );
            eventArgs.ReturnValue = originalResult;
            BrushEvenTileHeightEventArgs postEventArgs = new(EventHookPhase.Post, centerTileId, centerTileY, brushSize)
            {
                ReturnValue = originalResult
            };
            MapEditorR3EventHooks.OnEvenTileHeight.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    // void __fastcall c_game_editor_brush_delete(_DWORD *pTileManager, unsigned int centerTileX, unsigned int centerTileY, int brushSize)
    // 4C 8B DC 45 89 43 ? 53 41 55
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_editor_brush_delete_delegate(IntPtr pTileManager, int centerTileX, int centerTileY, int brushSize);
    internal static DetourHandle<c_game_editor_brush_delete_delegate> c_game_editor_brush_delete_hook = new();
    public unsafe static Int64 c_game_editor_brush_delete_hook_impl(IntPtr pTileManager, int centerTileX, int centerTileY, int brushSize)
    {
        LogHelper.Debug($"centerTileX={centerTileX}, centerTileY={centerTileY}, brushSize={brushSize}");

        if (DebugMenuManager.Instance._mirrorTool.DeleteMirrorEnabled)
        {
            GameTileManagerAPI gtm = GameTileManagerAPI.Instance;
            int centerTileId = gtm.GetTileId(centerTileX, centerTileY);
            MirrorTool.ExecuteMirrorAction(centerTileId, centerTileY, (mirroredId, mirroredX, mirroredY) =>
            {
                c_game_editor_brush_delete_hook.Original!(pTileManager, mirroredX, mirroredY, brushSize);
            });
        }

        BrushDeleteEventArgs eventArgs = new(EventHookPhase.Pre, centerTileX, centerTileY, brushSize);
        MapEditorR3EventHooks.OnDelete.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_editor_brush_delete_hook.Original!(
                pTileManager,
                eventArgs.CenterTileX,
                eventArgs.CenterTileY,
                eventArgs.BrushSize
            );
            eventArgs.ReturnValue = originalResult;
            BrushDeleteEventArgs postEventArgs = new(EventHookPhase.Post, centerTileX, centerTileY, brushSize)
            {
                ReturnValue = originalResult
            };
            MapEditorR3EventHooks.OnDelete.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    // __int64 __fastcall c_game_editor_spawn_unit(int *pTribeManager, __int16 dx0, __int16 a3, int tileX, int tileY, int playerId, int unitChimpType, int a8)
    // 48 89 5C 24 ?? 48 89 6C 24 ?? 56 57 41 54 41 55 41 56 48 83 EC ?? 48 63 9C 24
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_editor_spawn_unit_delegate(NativePointer<GameTribeManager> pTribeManager, Int16 a2, Int16 a3, int tileX, int tileY, int playerId, int unitChimpType, UInt32 a8);
    internal static DetourHandle<c_game_editor_spawn_unit_delegate> c_game_editor_spawn_unit_hook = new();
    public unsafe static Int64 c_game_editor_spawn_unit_hook_impl(NativePointer<GameTribeManager> pTribeManager, Int16 a2, Int16 a3, int tileX, int tileY, int playerId, int unitChimpType, UInt32 a8)
    {
        LogHelper.Debug($"centerTileId={pTribeManager.ToString()}, a2={a2}, a3={a3}, tileX={tileX}, tileY={tileY}, playerId={playerId}, unitChimpType={(eChimps)unitChimpType}, a8={a8}");

        if (DebugMenuManager.Instance._mirrorTool.UnitMirrorEnabled)
        {
            GameTileManagerAPI gtm = GameTileManagerAPI.Instance;
            int centerTileId = gtm.GetTileId(tileX, tileY);
            MirrorTool.ExecuteMirrorAction(centerTileId, tileY, (mirroredId, mirroredX, mirroredY) =>
            {
                c_game_editor_spawn_unit_hook.Original!(
                    pTribeManager,
                    a2,
                    a3,
                    mirroredX,
                    mirroredY,
                    playerId,
                    unitChimpType,
                    a8);
            });
        }

        UnitPlaceEventArgs eventArgs = new(EventHookPhase.Pre, a2, a3, tileX, tileY, playerId, (eChimps)unitChimpType, a8);
        MapEditorR3EventHooks.OnUnitPlace.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_editor_spawn_unit_hook.Original!(
                pTribeManager,
                eventArgs.Unknown1,
                eventArgs.Unknown2,
                eventArgs.TileX,
                eventArgs.TileY,
                eventArgs.PlayerId,
                (int)eventArgs.UnitType,
                eventArgs.Unknown3
            );
            eventArgs.ReturnValue = originalResult;
            UnitPlaceEventArgs postEventArgs = new(EventHookPhase.Post, a2, a3, tileX, tileY, playerId, (eChimps)unitChimpType, a8)
            {
                ReturnValue = originalResult
            };
            MapEditorR3EventHooks.OnUnitPlace.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }
}
