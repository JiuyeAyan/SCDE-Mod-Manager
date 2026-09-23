using RedBird.Core.Memory;
using RedBird.Core.Memory.Managed;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Runtime.CompilerServices;

namespace SHCDESE.API;

/// <summary>
/// Provides unrestricted live access to the native AIV system, village construction state, decoded layout buffers, coarse map, and shared coarse-search state.
/// </summary>
/// <remarks>
/// Returned pointers, references, and spans map game memory directly. Mutations take effect immediately. 
/// callers are responsible for synchronizing changes that must remain deterministic.
/// </remarks>
[LuaApiNamespace("AIV")]
public unsafe sealed class GameAIVManagerAPI
{
    public const int MAX_AIV_BANKS = 8;
    public const int MAX_AIV_VARIANTS_PER_BANK = 1_000;

    private static readonly Lazy<GameAIVManagerAPI> _lazy = new(() => new GameAIVManagerAPI());
    public static GameAIVManagerAPI Instance => _lazy.Value;

    private readonly AivSystem* _aivSystem;
    private readonly UInt64* _importedAivVariantPointers;
    private readonly Int32* _importedAivBankCustomFlags;

    /// <summary>
    /// Gold at or below this value activates the per-lord AIV build-rate delay.
    /// A hook of <c>c_game_ai_build_handler</c> must consume this value to override native behavior.
    /// </summary>
    [LuaApiExport("BuildDelayGoldCeiling")]
    public ManagedValue<Int32> BuildDelayGoldCeiling { get; }

    /// <summary>
    /// Gold strictly above this value advances AIV construction by two build steps per update.
    /// A hook of <c>c_game_ai_build_handler</c> must consume this value to override native behavior.
    /// </summary>
    [LuaApiExport("AcceleratedBuildGoldThreshold")]
    public ManagedValue<Int32> AcceleratedBuildGoldThreshold { get; }

    private GameAIVManagerAPI()
    {
        ValidateInteropLayouts();

        UInt64 aivSystemVA = GameGlobalsManager.Instance.AIVSystemVA;
        if (aivSystemVA == 0)
        {
            LogHelper.Error("AIVSystem VA is null! This should never happen!");
        }
        else
        {
            _aivSystem = (AivSystem*)aivSystemVA;
            LogHelper.Information($"AIVSystem: {new IntPtr(_aivSystem).ToString("X16")}");
        }

        UInt64 aivImportedVariantsVA = GameGlobalsManager.Instance.AIVImportedVariantsVA;
        if (aivImportedVariantsVA == 0)
        {
            LogHelper.Error("AIV data table VA is null! This should never happen!");
        }
        else
        {
            _importedAivVariantPointers = (UInt64*)aivImportedVariantsVA;
            _importedAivBankCustomFlags = (Int32*)(_importedAivVariantPointers + MAX_AIV_BANKS * MAX_AIV_VARIANTS_PER_BANK);

            LogHelper.Information($"Imported AIV variants: {new IntPtr(_importedAivVariantPointers).ToString("X16")}");
        }
    }

    private static void ValidateInteropLayouts()
    {
        if (sizeof(AivBuildStep) != AivBuildStep.SIZE)
            throw new TypeLoadException($"Invalid {nameof(AivBuildStep)} size: {sizeof(AivBuildStep):X}");

        if (sizeof(AivVillageState) != AivVillageState.SIZE)
            throw new TypeLoadException($"Invalid {nameof(AivVillageState)} size: {sizeof(AivVillageState):X}");

        if (sizeof(AivCoarseCell) != AivCoarseCell.SIZE)
            throw new TypeLoadException($"Invalid {nameof(AivCoarseCell)} size: {sizeof(AivCoarseCell):X}");

        if (sizeof(AivSystem) != AivSystem.SIZE)
            throw new TypeLoadException($"Invalid {nameof(AivSystem)} size: {sizeof(AivSystem):X}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativePointer<AivSystem> GetAIVSystem() => new NativePointer<AivSystem>(_aivSystem);

    /// <summary>
    /// Returns a raw pointer to the complete live native AIV system.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AivSystem* GetAIVSystemPointer() => _aivSystem;

    /// <summary>
    /// Returns all 8,000 native imported-variant pointer slots in bank-major order.
    /// Each nonzero value points to an encoded <see cref="Int16"/> AIV buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<UInt64> GetImportedAIVVariantPointers()
    {
        return _importedAivVariantPointers == null ? Span<UInt64>.Empty : new Span<UInt64>(_importedAivVariantPointers, MAX_AIV_BANKS * MAX_AIV_VARIANTS_PER_BANK);
    }

    /// <summary>
    /// Returns the live custom-data flag for each zero-based AIV bank.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetImportedAIVBankCustomFlags()
    {
        return _importedAivBankCustomFlags == null ? Span<Int32>.Empty : new Span<Int32>(_importedAivBankCustomFlags, MAX_AIV_BANKS);
    }

    public bool TryGetImportedAIVVariant(int bankIndex, int variantIndex, out Int16* data)
    {
        data = null;
        if (_importedAivVariantPointers == null
            || (UInt32)bankIndex >= MAX_AIV_BANKS
            || (UInt32)variantIndex >= MAX_AIV_VARIANTS_PER_BANK)
        {
            return false;
        }

        data = (Int16*)_importedAivVariantPointers[bankIndex * MAX_AIV_VARIANTS_PER_BANK + variantIndex];
        return data != null;
    }

    /// <summary>
    /// Counts the dense prefix of imported variants in one native AIV bank.
    /// </summary>
    public int GetImportedAIVVariantCount(int bankIndex)
    {
        if (_importedAivVariantPointers == null || (UInt32)bankIndex >= MAX_AIV_BANKS)
            return 0;

        UInt64* bank = _importedAivVariantPointers + bankIndex * MAX_AIV_VARIANTS_PER_BANK;
        int count = 0;
        while (count < MAX_AIV_VARIANTS_PER_BANK && bank[count] != 0)
            count++;

        return count;
    }

    /// <summary>
    /// Returns all nine native village records, including reserved slot zero.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<AivVillageState> GetVillageSlots()
    {
        return _aivSystem == null ? Span<AivVillageState>.Empty : new Span<AivVillageState>(&_aivSystem->ReservedVillageSlot, AivSystem.VILLAGE_SLOT_COUNT);
    }

    /// <summary>
    /// Returns native village slots one through eight, excluding reserved slot zero.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<AivVillageState> GetLiveVillageSlots()
    {
        return _aivSystem == null ? Span<AivVillageState>.Empty : new Span<AivVillageState>(&_aivSystem->VillageSlot1, AivSystem.LIVE_VILLAGE_SLOT_COUNT);
    }

    public bool TryGetVillageBySlot(int villageSlot, out AivVillageState* village)
    {
        village = null;
        if (_aivSystem == null || (UInt32)villageSlot >= AivSystem.VILLAGE_SLOT_COUNT)
            return false;

        village = &_aivSystem->ReservedVillageSlot + villageSlot;
        return true;
    }

    public bool TryGetVillageByPlayerId(int playerId, out AivVillageState* village)
    {
        village = null;
        if (_aivSystem == null || playerId < 1 || playerId > AivSystem.LAST_LIVE_VILLAGE_SLOT)
            return false;

        AivVillageState* current = &_aivSystem->VillageSlot1;
        for (int i = 0; i < AivSystem.LIVE_VILLAGE_SLOT_COUNT; i++, current++)
        {
            if (current->OwnerPlayerId == playerId)
            {
                village = current;
                return true;
            }
        }

        return false;
    }

    public Span<AivBuildStep> GetBuildSteps(int villageSlot)
    {
        if (!TryGetVillageBySlot(villageSlot, out AivVillageState* village))
            return Span<AivBuildStep>.Empty;

        return new Span<AivBuildStep>((AivBuildStep*)&village->BuildStepsBuffer[0], AivVillageState.BUILD_STEP_CAPACITY);
    }

    public bool TryGetBuildStep(int villageSlot, int buildStep, out AivBuildStep* step)
    {
        step = null;
        if (!TryGetVillageBySlot(villageSlot, out AivVillageState* village) || (UInt32)buildStep >= AivVillageState.BUILD_STEP_CAPACITY)
        {
            return false;
        }

        step = (AivBuildStep*)&village->BuildStepsBuffer[buildStep * AivBuildStep.SIZE];
        return true;
    }

    /// <summary>
    /// Returns whether the native build-step payload indexes the village's ordered map-tile
    /// buffer instead of containing one absolute packed map-tile ID.
    /// </summary>
    public static bool UsesOrderedMapTileBuffer(eMappers buildingType)
    {
        return buildingType == eMappers.MAPPER_WALL
            || buildingType == eMappers.MAPPER_CRENAL
            || buildingType == eMappers.MAPPER_CRENAL2
            || buildingType == eMappers.MAPPER_WOODWALL
            || buildingType == eMappers.MAPPER_PITCH_DITCH
            || buildingType == eMappers.MAPPER_MOAT;
    }

    /// <summary>
    /// Resolves a build step to its live map-tile sequence. For a normal building the returned
    /// one-element span aliases the step payload itself; grouped steps alias the village buffer.
    /// </summary>
    public Span<Int32> GetBuildStepMapTiles(int villageSlot, int buildStep)
    {
        if (!TryGetVillageBySlot(villageSlot, out AivVillageState* village) || (UInt32)buildStep >= AivVillageState.BUILD_STEP_CAPACITY)
        {
            return Span<Int32>.Empty;
        }

        AivBuildStep* step = (AivBuildStep*)&village->BuildStepsBuffer[buildStep * AivBuildStep.SIZE];
        if (!UsesOrderedMapTileBuffer(step->BuildingType))
            return new Span<Int32>(&step->MapTileIdOrBufferIndex, 1);

        int startIndex = step->MapTileIdOrBufferIndex;
        int count = step->TileCount;
        if (startIndex < 0
            || count <= 0
            || startIndex > AivVillageState.ORDERED_MAP_TILE_CAPACITY - count)
        {
            return Span<Int32>.Empty;
        }

        return new Span<Int32>(&village->OrderedMapTileIds[startIndex], count);
    }

    public Span<Int32> GetOrderedMapTileBuffer(int villageSlot)
    {
        if (!TryGetVillageBySlot(villageSlot, out AivVillageState* village))
            return Span<Int32>.Empty;

        return new Span<Int32>(&village->OrderedMapTileIds[0], AivVillageState.ORDERED_MAP_TILE_CAPACITY);
    }

    public Span<Int32> GetOrderedMapTiles(int villageSlot)
    {
        if (!TryGetVillageBySlot(villageSlot, out AivVillageState* village))
            return Span<Int32>.Empty;

        int count = village->OrderedMapTileCount;
        if (count < 0)
            count = 0;
        else if (count > AivVillageState.ORDERED_MAP_TILE_CAPACITY)
            count = AivVillageState.ORDERED_MAP_TILE_CAPACITY;

        return new Span<Int32>(&village->OrderedMapTileIds[0], count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<eMappers> GetBuildingTypeGrid()
    {
        return _aivSystem == null ? Span<eMappers>.Empty : new Span<eMappers>((eMappers*)&_aivSystem->BuildingTypeGrid[0], AivSystem.LAYOUT_GRID_CELL_COUNT);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetBuildStepGrid()
    {
        return _aivSystem == null ? Span<Int32>.Empty : new Span<Int32>(&_aivSystem->BuildStepGrid[0], AivSystem.LAYOUT_GRID_CELL_COUNT);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<eMappers> GetRotatedBuildingTypeGrid()
    {
        return _aivSystem == null ? Span<eMappers>.Empty : new Span<eMappers>((eMappers*)&_aivSystem->RotatedBuildingTypeGrid[0], AivSystem.LAYOUT_GRID_CELL_COUNT);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetRotatedBuildStepGrid()
    {
        return _aivSystem == null ? Span<Int32>.Empty : new Span<Int32>(&_aivSystem->RotatedBuildStepGrid[0], AivSystem.LAYOUT_GRID_CELL_COUNT);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetPauseFrameIndices()
    {
        return _aivSystem == null ? Span<Int32>.Empty : new Span<Int32>(&_aivSystem->PauseFrameIndices[0], AivSystem.PAUSE_FRAME_CAPACITY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetMiscItemPositions()
    {
        return _aivSystem == null ? Span<Int32>.Empty : new Span<Int32>(&_aivSystem->MiscItemPositionBuffer[0], AivSystem.MISC_ITEM_TYPE_CAPACITY * AivSystem.MISC_ITEM_POSITION_CAPACITY);
    }

    public Span<Int32> GetMiscItemPositions(int itemType)
    {
        if (_aivSystem == null || (UInt32)itemType >= AivSystem.MISC_ITEM_TYPE_CAPACITY)
            return Span<Int32>.Empty;

        return new Span<Int32>(&_aivSystem->MiscItemPositionBuffer[itemType * AivSystem.MISC_ITEM_POSITION_CAPACITY], AivSystem.MISC_ITEM_POSITION_CAPACITY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetMiscItemPositions(AivMiscItemType itemType)
    {
        return GetMiscItemPositions((int)itemType);
    }

    /// <summary>
    /// Returns the complete live coarse grid in native order. Native indexing is Y + 160 * X.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<AivCoarseCell> GetCoarseGrid()
    {
        return _aivSystem == null ? Span<AivCoarseCell>.Empty : new Span<AivCoarseCell>((AivCoarseCell*)&_aivSystem->CoarseGridBuffer[0], AivSystem.COARSE_GRID_CELL_COUNT);
    }

    public bool TryGetCoarseCell(int x, int y, out AivCoarseCell* cell)
    {
        cell = null;
        if (_aivSystem == null || (UInt32)x >= AivSystem.COARSE_GRID_WIDTH || (UInt32)y >= AivSystem.COARSE_GRID_HEIGHT)
        {
            return false;
        }

        int index = y + AivSystem.COARSE_GRID_WIDTH * x;
        cell = (AivCoarseCell*)&_aivSystem->CoarseGridBuffer[index * AivCoarseCell.SIZE];
        return true;
    }

    /// <summary>
    /// Returns the result most recently written by any native coarse-grid search.
    /// The shared result is overwritten by harassment and procedural placement searches.
    /// </summary>
    public bool TryGetCoarseSearchResult(out int x, out int y)
    {
        x = -1;
        y = -1;
        if (_aivSystem == null)
            return false;

        x = _aivSystem->CoarseSearchResultX;
        y = _aivSystem->CoarseSearchResultY;
        return (UInt32)x < AivSystem.COARSE_GRID_WIDTH && (UInt32)y < AivSystem.COARSE_GRID_HEIGHT;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Byte> GetUnknownCoarseState()
    {
        return _aivSystem == null ? Span<Byte>.Empty : new Span<Byte>(&_aivSystem->UnknownCoarseGridState[0], AivSystem.UNKNOWN_COARSE_STATE_SIZE);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetCoarseSearchQueueX()
    {
        return _aivSystem == null ? Span<Int32>.Empty : new Span<Int32>(&_aivSystem->CoarseSearchQueueX[0], AivSystem.COARSE_GRID_CELL_COUNT);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetCoarseSearchQueueY()
    {
        return _aivSystem == null ? Span<Int32>.Empty : new Span<Int32>(&_aivSystem->CoarseSearchQueueY[0], AivSystem.COARSE_GRID_CELL_COUNT);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Byte> GetPlacementVisitedMask()
    {
        return _aivSystem == null ? Span<Byte>.Empty : new Span<Byte>(&_aivSystem->PlacementVisitedMask[0], AivSystem.LAYOUT_GRID_CELL_COUNT);
    }

    /// <summary>
    /// Imports one encoded AIV variant into a zero-based native AIV bank.
    /// </summary>
    public bool ImportAIV(int bankIndex, int variantIndex, Int16[] data, bool isCustom)
    {
        if ((UInt32)bankIndex >= MAX_AIV_BANKS
            || (UInt32)variantIndex >= MAX_AIV_VARIANTS_PER_BANK
            || data == null
            || data.Length == 0)
        {
            return false;
        }

        EngineInterface.ImportAIV(bankIndex, variantIndex, data, isCustom ? 1 : 0);
        return true;
    }

    /// <summary>
    /// Calls the native import function without managed range checks.
    /// Passing a bank outside 0..7 invokes the native imported-AIV cleanup path.
    /// </summary>
    public void ImportAIVUnchecked(int bankIndex, int variantIndex, Int16* data, int elementCount, int isCustom)
    {
        EngineInterface.DLL_ImportAIV(bankIndex, variantIndex, data, elementCount, isCustom);
    }

    /// <summary>
    /// Frees every densely stored imported AIV variant through the game's cleanup entry point.
    /// </summary>
    public void ClearImportedAIVs()
    {
        EngineInterface.InitAIVLoading();
    }
}
