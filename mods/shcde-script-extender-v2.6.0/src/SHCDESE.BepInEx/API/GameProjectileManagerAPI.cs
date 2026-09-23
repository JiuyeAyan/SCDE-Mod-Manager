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
/// Provides a high-level API for interacting with game projectiles.
/// </summary>
/// <remarks>
/// This class is a singleton that serves as the primary entry point for creating, deleting,
/// and querying projectiles in the game world. It offers both direct manipulation methods
/// and a high-performance query system for finding projectiles based on various criteria.
/// </remarks>
[LuaApiNamespace("Projectile")]
public unsafe sealed class GameProjectileManagerAPI
{
    private static readonly Lazy<GameProjectileManagerAPI> _lazy = new(() => new GameProjectileManagerAPI());
    public static GameProjectileManagerAPI Instance => _lazy.Value;

    internal const int NUM_PREALLOC_PROJECTILES = 6000;

    private SimpleNativeArray<GameProjectile> _projectileArray;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameProjectileManagerAPI"/> class.
    /// This constructor is private to enforce the singleton pattern.
    /// </summary>
    private GameProjectileManagerAPI()
    {
        _projectileArray = new SimpleNativeArray<GameProjectile>((byte*)GameGlobalsManager.Instance.GameProjectilesManagerVA, NUM_PREALLOC_PROJECTILES);

        LogHelper.Information($"_projectileArray: {GameGlobalsManager.Instance.GameProjectilesManagerVA.ToString("X16")}");
    }

    /// <summary>
    /// Returns the underlying array of game projectile managed by this instance.
    /// </summary>
    /// <returns>A <see cref="SimpleNativeArray{GameProjectile}"/> containing all projectile.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimpleNativeArray<GameProjectile> GetProjectilesArray()
    {
        return _projectileArray;
    }

    /// <summary>
    /// Returns a span representing the current collection of projectile managed by the instance.
    /// </summary>
    /// <returns>A <see cref="Span{GameProjectile}"/> containing the projectile.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<GameProjectile> GetProjectilesAsSpan()
    {
        return _projectileArray.AsSpan();
    }

    /// <summary>
    /// Attempts to retrieve a direct pointer to a projectile by its ID.
    /// </summary>
    /// <param name="projectileId">The unique identifier of the projectile.</param>
    /// <param name="projectile">When this method returns, contains a pointer to the projectile if found; otherwise, null.</param>
    /// <returns><c>true</c> if the projectile was found and is within the valid array bounds; otherwise, <c>false</c>.</returns>
    public bool TryGetProjectileById(int projectileId, out GameProjectile* projectile)
    {
        projectile = null;
        if (!IsValidId(projectileId))
        {
            LogHelper.Error($"Tried to access projectile index that was out of range: [{projectileId}/{_projectileArray.Length}]");
            return false;
        }

        if (_projectileArray._array == null)
            return false;

        projectile = &_projectileArray._array[projectileId];
        return true;
    }

    /// <summary>
    /// Attempts to retrieve a safe, wrapped pointer to a projectile by their ID.
    /// </summary>
    /// <param name="id">The unique identifier of the projectile.</param>
    /// <param name="projectile">When this method returns, contains a pointer to the projectile if found; otherwise, null.</param>
    /// <returns><c>true</c> if the projectile ID was valid; otherwise, <c>false</c>.</returns>
    public bool TryGetProjectileByIdEx(int id, out NativePointer<GameProjectile> projectile)
    {
        bool result = TryGetProjectileById(id, out GameProjectile* projectilePtr);
        projectile = new NativePointer<GameProjectile>(projectilePtr);
        return result;
    }

    /// <summary>
    /// Creates and spawns a new projectile in the game world.
    /// </summary>
    /// <param name="sourceUnitId">The ID of the unit firing the projectile.</param>
    /// <param name="sourcePlayerId">The ID of the player owning the projectile.</param>
    /// <param name="sourceWorldTileX">The starting world X-coordinate of the projectile.</param>
    /// <param name="sourceWorldTileY">The starting world Y-coordinate of the projectile.</param>
    /// <param name="sourceElevation">The starting elevation of the projectile.</param>
    /// <param name="targetWorldTileX">The target world X-coordinate.</param>
    /// <param name="targetWorldTileY">The target world Y-coordinate.</param>
    /// <param name="targetElevation">The target elevation.</param>
    /// <param name="projectileType">The type of projectile to create (e.g., arrow, bolt).</param>
    /// <param name="targetUnitId">The ID of the unit being targeted. Can be 0 if targeting terrain.</param>
    /// <returns>The unique ID of the newly created projectile.</returns>
    /// <example>
    /// Projectile_Create(0, 0, 400, 400, 8, 405, 405, 8, eProjectileType.CatapultRocks, 0)
    /// </example>
    [LuaApiExport("Create")]
    public Int64 CreateProjectile(
        int sourceUnitId, int sourcePlayerId,
        int sourceWorldTileX, int sourceWorldTileY, int sourceElevation,
        int targetWorldTileX, int targetWorldTileY, int targetElevation,
        ProjectileType projectileType,
        int targetUnitId)
    {
        LogHelper.Debug($"Creating projectile: srcUnitId={sourceUnitId}, srcPlayerId={sourcePlayerId}, src=({sourceWorldTileX}, {sourceWorldTileY}, {sourceElevation}), dest=({targetWorldTileX}, {targetWorldTileY}, {targetElevation}), projectileType={projectileType}, targetUnitId={targetUnitId}");

        return BulkProjectileDetours.c_game_projectile_spawn_hook_impl(_projectileArray._array, sourceUnitId, (Int16)sourcePlayerId, sourcePlayerId,
            sourceWorldTileX, sourceWorldTileY, sourceElevation,
            targetWorldTileX, targetWorldTileY, targetElevation,
            projectileType, targetUnitId);
    }

    /// <summary>
    /// Safely marks a projectile for deletion by the game engine.
    /// </summary>
    /// <param name="projectileId">The ID of the projectile to delete.</param>
    /// <returns><c>true</c> if the projectile was found and marked for deletion; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This is the recommended way to delete a projectile. It changes its state, allowing the
    /// game engine to clean it up gracefully on a subsequent frame, preventing crashes.
    /// </remarks>
    /// <seealso cref="DeleteProjectile(int)"/>
    [LuaApiExport("DeleteSafe")]
    public bool DeleteProjectileSafe(int projectileId)
    {
        if (!TryGetProjectileById(projectileId, out GameProjectile* projectile))
        {
            LogHelper.Warning($"Could not find projectile by id: {projectileId}");
            return false;
        }
        projectile->r_AliveState = AliveState.MarkedForDeletion;

        LogHelper.Debug($"Projectile ({projectileId}/{new IntPtr(&projectile->r_AliveState).ToString("X16")}) -- Marked for deletion");
        return true;
    }

    /// <summary>
    /// Immediately deletes a projectile from the game.
    /// </summary>
    /// <param name="projectileId">The ID of the projectile to delete.</param>
    /// <remarks>
    /// This method is more direct but potentially less safe than <see cref="DeleteProjectileSafe"/>.
    /// Use with caution, as immediate deletion could cause issues if other game systems are
    /// still referencing the projectile.
    /// </remarks>
    /// <seealso cref="DeleteProjectileSafe(int)"/>
    [LuaApiExport("Delete")]
    public void DeleteProjectile(int projectileId)
    {
        if (!IsValid(projectileId))
        {
            LogHelper.Warning($"Tried to delete invalid entity: {projectileId}");
            return;
        }
        BulkProjectileDetours.c_game_projectile_delete_hook_impl(_projectileArray._array, projectileId);
    }

    /// <summary>
    /// Returns the unitId that shot this projectile
    /// </summary>
    /// <param name="projectileId">The ID of the projectile</param>
    /// <returns>The unitId that shot this projectile. Otherwise -1.</returns>
    [LuaApiExport("GetSourceUnit")]
    public int GetSourceUnit(int projectileId)
    {
        if (!TryGetProjectileById(projectileId, out GameProjectile* projectile))
        {
            LogHelper.Warning($"Could not find projectile by id: {projectileId}");
            return -1;
        }
        return projectile->r_SourceUnitId;
    }

    /// <summary>
    /// Returns the unitId that this projectile targets
    /// </summary>
    /// <param name="projectileId">The ID of the projectile</param>
    /// <returns>The unitId that this projectile targets. Otherwise -1.</returns>
    [LuaApiExport("GetTargetUnit")]
    public int GetTargetUnit(int projectileId)
    {
        if (!TryGetProjectileById(projectileId, out GameProjectile* projectile))
        {
            LogHelper.Warning($"Could not find projectile by id: {projectileId}");
            return -1;
        }
        return projectile->r_TargetUnidId;
    }

    /// <summary>
    /// Returns the player that owns this projectile
    /// </summary>
    /// <param name="projectileId">The ID of the projectile</param>
    /// <returns>The player that owns this projectile. Otherwise -1.</returns>
    [LuaApiExport("GetSourcePlayer")]
    public int GetSourcePlayer(int projectileId)
    {
        if (!TryGetProjectileById(projectileId, out GameProjectile* projectile))
        {
            LogHelper.Warning($"Could not find projectile by id: {projectileId}");
            return -1;
        }
        return (int)projectile->r_PlayerSourceId;
    }

    /// <summary>
    /// Checks if a projectile id is valid and exists.
    /// </summary>
    /// <param name="projectileId">The ID of the projectile to check.</param>
    /// <returns><c>true</c> if the projectile was found and is valid; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsValid")]
    public bool IsValid(int projectileId)
    {
        if (!IsValidId(projectileId))
            return false;

        if (!TryGetProjectileById(projectileId, out GameProjectile* projectile))
        {
            LogHelper.Warning($"Could not find projectile by id: {projectileId}");
            return false;
        }
        return (int)projectile->r_AliveState != 0;
    }

    /// <summary>
    /// Checks if a projectile id is valid.
    /// </summary>
    /// <param name="projectileId">The ID to check.</param>
    /// <returns><c>true</c> if the projectile id is valid; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsValidId")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValidId(int projectileId)
    {
        if (projectileId <= 0 || projectileId >= _projectileArray.Length)
            return false;

        return true;
    }

    #region Query System

    /// <summary>
    /// Begins a high-performance query over all possible projectile slots.
    /// </summary>
    public GameStructQuery<GameProjectile> QueryProjectiles()
    {
        return new GameStructQuery<GameProjectile>(_projectileArray._array + 1, _projectileArray.Length - 1);
    }

    /// <summary>
    /// The core query execution method. All public query functions delegate to this.
    /// </summary>
    /// <param name="results">The list to save the found results in.</param>
    /// <param name="basePredicate">The search query predicate</param>
    /// <param name="stateFilter">The projectile state to look out for <see cref="AliveState"/></param>
    /// <param name="projType">The projectile type to look out for <see cref="ProjectileType"/></param>
    public void ExecuteQuery(List<int> results, RefPredicate<GameProjectile> basePredicate, AliveState? stateFilter, ProjectileType? projType)
    {
        GameStructQuery<GameProjectile> query = QueryProjectiles().Where(basePredicate);

        if (stateFilter.HasValue)
        {
            AliveState state = stateFilter.Value;
            query = query.Where((in GameProjectile proj) => proj.r_AliveState == state);
        }

        if (projType.HasValue)
        {
            ProjectileType type = projType.Value;
            query = query.Where((in GameProjectile proj) => proj.r_ProjectileType == type);
        }

        query.ToIdList(results);
    }

    /// <summary>
    /// A set of common, reusable predicates for convenience.
    /// </summary>
    public static class ProjectilePredicates
    {
        public static readonly RefPredicate<GameProjectile> IsAlive = (in GameProjectile proj) => proj.r_AliveState == AliveState.IsAlive;

        public static readonly RefPredicate<GameProjectile> IsDead = (in GameProjectile proj) => proj.r_AliveState == AliveState.MarkedForDeletion;

        // Predicate to match any unit, used as a default for generic queries.
        public static readonly RefPredicate<GameProjectile> Any = (in GameProjectile unit) => true;

        public static RefPredicate<GameProjectile> IsOfType(ProjectileType projType) => (in GameProjectile proj) => proj.r_ProjectileType == projType;

        /// <summary>
        /// Matches projectiles whose current tile falls within the specified rectangle.
        /// Reads position fields directly via the <c>in</c> parameter instead of calling
        /// <c>CurrentTilePosition()</c>, which uses a <c>fixed</c> statement that pins
        /// a stack copy rather than the native array element when the struct is on the
        /// unmanaged heap.
        /// </summary>
        public static RefPredicate<GameProjectile> IsWithinRect(int x, int y, int width, int height) =>
            (in GameProjectile p) =>
                p.r_CurrentTileX >= x && p.r_CurrentTileX < x + width &&
                p.r_CurrentTileY >= y && p.r_CurrentTileY < y + height;

        /// <summary>
        /// Matches projectiles whose current tile falls within the specified sphere (circle).
        /// Uses a pre-computed squared radius to avoid <c>Math.Sqrt</c> per entry.
        /// Same rationale as <see cref="IsWithinRect"/> for avoiding <c>CurrentTilePosition()</c>.
        /// </summary>
        public static RefPredicate<GameProjectile> IsWithinSphere(int cx, int cy, int radius)
        {
            int rSq = radius * radius;
            return (in GameProjectile p) =>
            {
                int dx = p.r_CurrentTileX - cx;
                int dy = p.r_CurrentTileY - cy;
                return dx * dx + dy * dy <= rSq;
            };
        }
    }

    /// <summary>
    /// Fills a list with IDs of all projs, with optional filters (no spatial constraint).
    /// </summary>
    public void GetAllProjectiles(List<int> results, AliveState? stateFilter = null, ProjectileType? projType = null)
    {
        ExecuteQuery(results, ProjectilePredicates.Any, stateFilter, projType);
    }

    #endregion

}
