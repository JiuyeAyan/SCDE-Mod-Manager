using RedBird.Core.Memory;
using SHCDESE.Detours;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Interop.Query;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SHCDESE.API;

/// <summary>
/// Provides a high-level API for creating, querying, and removing pitch (tar-trap) tiles.
/// </summary>
/// <remarks>
/// <para>
/// Pitch tiles are stored in a flat array of up to <see cref="MAX_PITCH_ENTRIES"/> slots
/// embedded directly inside the TileManager struct at offset <c>0x2038E48</c>.
/// Pitch IDs are <b>one-based</b>: ID <c>n</c> is stored at array element <c>n - 1</c>, so ID
/// <c>1</c> occupies the first slot. The allocator hands out <c>1</c> to
/// <c>MAX_PITCH_ENTRIES - 1</c>.
/// </para>
/// <para>
/// In addition to the array entry, creating a pitch tile writes to three tile grids:
/// <list type="bullet">
///   <item><see cref="TilePropertyFlag.PitchTrap"/> is OR'd into the <c>LogicGrid</c>.</item>
///   <item>Display bits <c>0x4000</c> and <c>0x2000</c> are cleared in <c>MiscDisplayGrid</c>.</item>
///   <item>The tile height is decremented by 4 (clamped to 0) in <c>HeightGrid</c>.</item>
///   <item><c>UnknownGrid2</c> (offset <c>0xA20D40</c>) is set to <c>2</c>.</item>
/// </list>
/// </para>
/// </remarks>
[LuaApiNamespace("Pitch")]
public unsafe sealed class GamePitchManagerAPI
{
    private static readonly Lazy<GamePitchManagerAPI> _lazy = new(() => new GamePitchManagerAPI());
    public static GamePitchManagerAPI Instance => _lazy.Value;

    /// <summary>Maximum number of simultaneous pitch entries the game supports.</summary>
    public const int MAX_PITCH_ENTRIES = 4000;

    // Offsets inside the TileManager struct
    private const UInt64 PitchArrayBaseOffset = 0x2038E48;
    private const UInt64 PitchArrayCountOffset = 0x204C6C4;
    private const UInt64 PitchSentinelRelative = 0x18;

    private readonly SimpleNativeArray<GamePitchDescriptor> _pitchArray;
    private readonly UInt64 _tileManagerVA;

    private GamePitchManagerAPI()
    {
        _tileManagerVA = GameGlobalsManager.Instance.GameTileManagerVA;

        byte* arrayBase = (byte*)(_tileManagerVA + PitchArrayBaseOffset);
        _pitchArray = new SimpleNativeArray<GamePitchDescriptor>(arrayBase, MAX_PITCH_ENTRIES);

        LogHelper.Information($"PitchArray base: {new IntPtr(arrayBase).ToString("X16")}");
        LogHelper.Information($"PitchArray count ptr: {(_tileManagerVA + PitchArrayCountOffset).ToString("X16")}");
    }

    /// <summary>Pointer to the game's high-water mark for the pitch array.</summary>
    private ref uint PitchArrayCount => ref *(uint*)(_tileManagerVA + PitchArrayCountOffset);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidId(int pitchId) => pitchId > 0 && pitchId <= MAX_PITCH_ENTRIES;

    internal int FindFreePitchSlotInternal()
    {
        byte* sentinelBase = (byte*)(_tileManagerVA + PitchArrayBaseOffset + PitchSentinelRelative);
        int stride = sizeof(GamePitchDescriptor);
        for (int i = 1; i < MAX_PITCH_ENTRIES; i++)
        {
            UInt16 sentinel = *(UInt16*)(sentinelBase + stride * (i - 1));
            if (sentinel == 0)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Returns the underlying native pitch array.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimpleNativeArray<GamePitchDescriptor> GetPitchArray() => _pitchArray;

    /// <summary>
    /// Returns a <see cref="Span{T}"/> over the entire pitch array.
    /// Modifications to the span write directly to game memory.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<GamePitchDescriptor> GetPitchArrayAsSpan() => _pitchArray.AsSpan();

    /// <summary>
    /// Attempts to retrieve a direct, raw pointer to a pitch entry by its slot ID.
    /// </summary>
    /// <param name="pitchId">The one-based pitch ID (1 to <see cref="MAX_PITCH_ENTRIES"/> - 1),
    /// as returned by <see cref="CreatePitch"/> or by a query. Resolved as <c>_array[pitchId - 1]</c>.</param>
    /// <param name="entry">
    /// On success, a raw pointer to the entry in game memory; <c>null</c> otherwise.
    /// Writing through this pointer modifies the game state immediately.
    /// </param>
    /// <returns><c>true</c> if the ID is valid and the array is mapped; otherwise <c>false</c>.</returns>
    public bool TryGetPitchById(int pitchId, out GamePitchDescriptor* entry)
    {
        entry = null;

        if (!IsValidId(pitchId))
        {
            LogHelper.Error($"Tried to access pitch index out of range: [{pitchId}/{MAX_PITCH_ENTRIES}]");
            return false;
        }

        if (_pitchArray._array == null)
            return false;

        entry = &_pitchArray._array[pitchId - 1];
        return true;
    }

    /// <summary>
    /// Attempts to retrieve a safe, wrapped pointer to a pitch entry by its slot ID.
    /// </summary>
    /// <param name="pitchId">The one-based pitch ID (1 to <see cref="MAX_PITCH_ENTRIES"/> - 1),
    /// as returned by <see cref="CreatePitch"/> or by a query. Resolved as <c>_array[pitchId - 1]</c>.</param>
    /// <param name="entry">
    /// On success, a <see cref="NativePointer{GamePitchDescriptor}"/> wrapping the entry; otherwise an invalid pointer.
    /// </param>
    /// <returns><c>true</c> if the ID is valid and the array is mapped; otherwise <c>false</c>.</returns>
    public bool TryGetPitchByIdEx(int pitchId, out NativePointer<GamePitchDescriptor> entry)
    {
        bool result = TryGetPitchById(pitchId, out GamePitchDescriptor* pitchPtr);
        entry = new NativePointer<GamePitchDescriptor>(pitchPtr);
        return result;
    }

    /// <summary>
    /// Creates a pitch tile at the specified map coordinates for the given player.
    /// </summary>
    /// <param name="tileX">The X coordinate of the target tile.</param>
    /// <param name="tileY">The Y coordinate of the target tile.</param>
    /// <param name="playerId">The player ID to assign as owner (1-based).</param>
    /// <returns>
    /// The allocated pitch slot ID (1-3999) on success, or <c>-1</c> if the array is full.
    /// </returns>
    /// <example>
    /// <code>
    /// local id = Pitch_Create(400, 400, 1)
    /// if id > 0 then
    ///     print("Pitch created, slot=" .. id)
    /// end
    /// </code>
    /// </example>
    [LuaApiExport("Create")]
    public int CreatePitch(int tileX, int tileY, int playerId)
    {
        return (int)BulkBuildingDetours.c_game_player_build_pitch_ditch_hook_impl((IntPtr)_tileManagerVA, (short)playerId, tileX, tileY);
    }

    /// <summary>
    /// Removes the pitch entry at the given slot ID and reverses all tile-grid side effects.
    /// </summary>
    /// <param name="pitchId">The slot ID returned by <see cref="CreatePitch"/>.</param>
    /// <example>
    /// <code>
    /// Pitch_Remove(id)
    /// </code>
    /// </example>
    [LuaApiExport("Remove")]
    public void RemovePitch(int pitchId)
    {
        BulkBuildingDetours.c_game_player_remove_pitch_ditch_hook_impl((IntPtr)_tileManagerVA, pitchId);
    }

    /// <summary>
    /// Sets a pitch trap state.
    /// </summary>
    /// <param name="pitchId">The pitch id to set</param>
    /// <param name="state">The new state</param>
    [LuaApiExport("SetState")]
    public void SetState(int pitchId, PitchState state)
    {
        if (!TryGetPitchById(pitchId, out GamePitchDescriptor* pitch))
        {
            LogHelper.Warning($"Could not find pitch by id: {pitchId}");
            return;
        }
        pitch->State = state;
    }

    /// <summary>
    /// Sets a pitch trap state.
    /// </summary>
    /// <param name="pitchId">The pitch id to set</param>
    /// <returns>The state; Returns <see cref="PitchState.None"/> on error.</returns>
    [LuaApiExport("GetState")]
    public PitchState SetState(int pitchId)
    {
        if (!TryGetPitchById(pitchId, out GamePitchDescriptor* pitch))
        {
            LogHelper.Warning($"Could not find pitch by id: {pitchId}");
            return PitchState.None;
        }
        return pitch->State;
    }

    // -------------------------------------------------------------------------
    // Query system
    // -------------------------------------------------------------------------

    /// <summary>
    /// Begins a high-performance query over all pitch slots.
    /// </summary>
    /// <returns>A <see cref="GameStructQuery{GamePitchDescriptor}"/> to chain predicates onto.</returns>
    public GameStructQuery<GamePitchDescriptor> QueryPitch()
    {
        return new GameStructQuery<GamePitchDescriptor>(_pitchArray._array, _pitchArray.Length);
    }

    /// <summary>
    /// The core query execution method. All public query functions delegate to this.
    /// </summary>
    /// <param name="results">The list to be cleared and filled with pitch slot IDs.</param>
    /// <param name="basePredicate">The primary search predicate applied first.</param>
    /// <param name="ownerFilter">Optional: additionally filters by exact owning player ID.</param>
    /// <param name="relationship">Optional: additionally filters by player relationship.</param>
    /// <param name="povPlayerId">Optional: POV player for relationship filtering. Only used when <paramref name="relationship"/> is not <see cref="PlayerRelationship.Any"/>.</param>
    public void ExecuteQuery(
        List<int> results,
        RefPredicate<GamePitchDescriptor> basePredicate,
        int? ownerFilter = null,
        PlayerRelationship? relationship = PlayerRelationship.Any,
        int? povPlayerId = 1)
    {
        GameStructQuery<GamePitchDescriptor> query = QueryPitch().Where(basePredicate);

        if (ownerFilter.HasValue)
        {
            int owner = ownerFilter.Value;
            query = query.Where((in GamePitchDescriptor p) => p.OwnerId == owner);
        }

        if (relationship != PlayerRelationship.Any && povPlayerId.HasValue)
        {
            switch (relationship)
            {
                case PlayerRelationship.Allied:
                    query = query.Where(PitchPredicates.IsAlliedTo(povPlayerId.Value));
                    break;
                case PlayerRelationship.Enemy:
                    query = query.Where(PitchPredicates.IsEnemyTo(povPlayerId.Value));
                    break;
                case PlayerRelationship.Self:
                    query = query.Where(PitchPredicates.IsOwnedBy(povPlayerId.Value));
                    break;
            }
        }

        query.ToIdList(results);
    }

    /// <summary>
    /// A set of common, reusable predicates for pitch queries.
    /// </summary>
    /// <remarks>
    /// Spatial predicates (<see cref="IsWithinRect"/>, <see cref="IsWithinSphere"/>) read
    /// <see cref="GamePitchDescriptor.TileX"/> and <see cref="GamePitchDescriptor.TileY"/> directly
    /// rather than delegating to the generic <c>SpatialPredicates</c> helper. This is necessary because
    /// <c>SpatialPredicates</c> calls <c>CurrentTilePosition()</c> which uses a <c>fixed</c> statement
    /// on a struct field. When the struct is already on the unmanaged heap (inside the native pitch array)
    /// and is passed as an <c>in</c> parameter through <see cref="GameStructQuery{T}"/>, the <c>fixed</c>
    /// expression pins a stack copy rather than the actual array element, yielding stale/garbage
    /// coordinates and causing incorrect spatial results.
    /// </remarks>
    public static class PitchPredicates
    {
        /// <summary>Matches any alive pitch slot.</summary>
        public static readonly RefPredicate<GamePitchDescriptor> IsAlive = static (in GamePitchDescriptor p) => p.IsAlive;

        /// <summary>Matches every slot regardless of state. Used as a passthrough base predicate.</summary>
        public static readonly RefPredicate<GamePitchDescriptor> Any = static (in GamePitchDescriptor p) => true;

        /// <summary>Matches alive pitch entries owned by <paramref name="playerId"/>.</summary>
        public static RefPredicate<GamePitchDescriptor> IsOwnedBy(int playerId) => (in GamePitchDescriptor p) => p.IsAlive && p.OwnerId == playerId;

        /// <summary>Matches alive pitch entries whose owner is allied to <paramref name="playerId"/>.</summary>
        public static RefPredicate<GamePitchDescriptor> IsAlliedTo(int playerId) => (in GamePitchDescriptor p) => p.IsAlive && GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(p.OwnerId, playerId);

        /// <summary>Matches alive pitch entries whose owner is an enemy of <paramref name="playerId"/>.</summary>
        public static RefPredicate<GamePitchDescriptor> IsEnemyTo(int playerId) => (in GamePitchDescriptor p) => p.IsAlive && !GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(p.OwnerId, playerId);

        /// <summary>Matches alive pitch entries located at the exact tile coordinate.</summary>
        public static RefPredicate<GamePitchDescriptor> IsAtTile(int tileX, int tileY) => (in GamePitchDescriptor p) => p.IsAlive && p.TileX == tileX && p.TileY == tileY;

        /// <summary>Matches alive pitch entries whose global ID equals <paramref name="globalId"/>.</summary>
        public static RefPredicate<GamePitchDescriptor> HasGlobalId(uint globalId) => (in GamePitchDescriptor p) => p.IsAlive && p.GlobalId == globalId;

        /// <summary>
        /// Matches alive pitch entries whose tile falls within the specified rectangle.
        /// Reads <see cref="GamePitchDescriptor.TileX"/> and <see cref="GamePitchDescriptor.TileY"/>
        /// directly via the <c>in</c> parameter to avoid any unsafe pointer aliasing.
        /// </summary>
        /// <param name="x">Top-left X coordinate (inclusive).</param>
        /// <param name="y">Top-left Y coordinate (inclusive).</param>
        /// <param name="width">Width of the rectangle in tiles.</param>
        /// <param name="height">Height of the rectangle in tiles.</param>
        public static RefPredicate<GamePitchDescriptor> IsWithinRect(int x, int y, int width, int height) =>
            (in GamePitchDescriptor p) =>
                p.IsAlive &&
                p.TileX >= x && p.TileX < x + width &&
                p.TileY >= y && p.TileY < y + height;

        /// <summary>
        /// Matches alive pitch entries whose tile falls within the specified sphere (circle).
        /// Uses a pre-computed squared radius to avoid a <c>Math.Sqrt</c> per entry.
        /// Reads <see cref="GamePitchDescriptor.TileX"/> and <see cref="GamePitchDescriptor.TileY"/>
        /// directly via the <c>in</c> parameter to avoid any unsafe pointer aliasing.
        /// </summary>
        /// <param name="cx">Center X coordinate.</param>
        /// <param name="cy">Center Y coordinate.</param>
        /// <param name="radius">Radius in tiles (inclusive boundary).</param>
        public static RefPredicate<GamePitchDescriptor> IsWithinSphere(int cx, int cy, int radius)
        {
            int rSq = radius * radius;
            return (in GamePitchDescriptor p) =>
            {
                if (!p.IsAlive) return false;
                int dx = p.TileX - cx;
                int dy = p.TileY - cy;
                return dx * dx + dy * dy <= rSq;
            };
        }
    }

    /// <summary>
    /// Fills a list with slot IDs of pitch tiles within a rectangular area, with optional filters.
    /// </summary>
    /// <param name="results">The list to be cleared and filled with pitch slot IDs.</param>
    /// <param name="x">Top-left X coordinate of the rectangle.</param>
    /// <param name="y">Top-left Y coordinate of the rectangle.</param>
    /// <param name="width">Width of the rectangle in tiles.</param>
    /// <param name="height">Height of the rectangle in tiles.</param>
    /// <param name="ownerFilter">Optional: filters by owning player ID.</param>
    /// <param name="relationship">Optional: filters by player relationship.</param>
    /// <param name="povPlayerId">Optional: POV player for relationship filtering.</param>
    public void GetPitchWithinRect(
        List<int> results,
        int x, int y, int width, int height,
        int? ownerFilter = null,
        PlayerRelationship? relationship = PlayerRelationship.Any,
        int? povPlayerId = 1)
    {
        ExecuteQuery(results, PitchPredicates.IsWithinRect(x, y, width, height), ownerFilter, relationship, povPlayerId);
    }

    /// <summary>
    /// Fills a list with slot IDs of pitch tiles within a spherical area, with optional filters.
    /// </summary>
    /// <param name="results">The list to be cleared and filled with pitch slot IDs.</param>
    /// <param name="x">Center X coordinate.</param>
    /// <param name="y">Center Y coordinate.</param>
    /// <param name="radius">Radius in tiles.</param>
    /// <param name="ownerFilter">Optional: filters by owning player ID.</param>
    /// <param name="relationship">Optional: filters by player relationship.</param>
    /// <param name="povPlayerId">Optional: POV player for relationship filtering.</param>
    public void GetPitchWithinSphere(
        List<int> results,
        int x, int y, int radius,
        int? ownerFilter = null,
        PlayerRelationship? relationship = PlayerRelationship.Any,
        int? povPlayerId = 1)
    {
        ExecuteQuery(results, PitchPredicates.IsWithinSphere(x, y, radius), ownerFilter, relationship, povPlayerId);
    }

    /// <summary>
    /// Fills a list with slot IDs of all pitch tiles, with optional filters.
    /// </summary>
    /// <param name="results">The list to be cleared and filled with pitch slot IDs.</param>
    /// <param name="ownerFilter">Optional: filters by owning player ID.</param>
    /// <param name="relationship">Optional: filters by player relationship.</param>
    /// <param name="povPlayerId">Optional: POV player for relationship filtering.</param>
    public void GetAllPitch(
        List<int> results,
        int? ownerFilter = null,
        PlayerRelationship? relationship = PlayerRelationship.Any,
        int? povPlayerId = 1)
    {
        ExecuteQuery(results, PitchPredicates.IsAlive, ownerFilter, relationship, povPlayerId);
    }
}