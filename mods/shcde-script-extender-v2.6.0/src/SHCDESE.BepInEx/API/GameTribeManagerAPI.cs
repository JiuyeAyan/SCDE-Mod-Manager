using RedBird.Core.Memory;
using SHCDESE.Detours;
using SHCDESE.Extensions;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Interop.Query;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SHCDESE.API;

/// <summary>
/// Provides a high-level API for interacting with game tribes (groups of units).
/// </summary>
/// <remarks>
/// This class is a singleton that serves as the primary entry point for creating, managing,
/// and issuing commands to tribes. Tribes are the core mechanism for controlling groups of units,
/// handling their AI, formations, and collective orders.
/// </remarks>
[LuaApiNamespace("Tribe")]
public unsafe sealed class GameTribeManagerAPI
{
    private static readonly Lazy<GameTribeManagerAPI> _lazy = new(() => new GameTribeManagerAPI());
    public static GameTribeManagerAPI Instance => _lazy.Value;

    /// <summary>The maximum number of tribes the game pre-allocates memory for.</summary>
    internal const int NUM_PREALLOC_TRIBES = 4500;

    internal GameTribeManager* _tribeManager;
    internal SimpleNativeArray<GameTribe> _tribesArray;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameTribeManagerAPI"/> class.
    /// This constructor is private to enforce the singleton pattern.
    /// </summary>
    private GameTribeManagerAPI()
    {
        _tribeManager = (GameTribeManager*)GameGlobalsManager.Instance.GameTribeManagerVA;
        _tribesArray = new SimpleNativeArray<GameTribe>((byte*)&_tribeManager->GameTribeArray, NUM_PREALLOC_TRIBES);

        LogHelper.Information($"_tribeManager: {new IntPtr(_tribeManager).ToString("X16")}");
        LogHelper.Information($"_tribesArray: {new IntPtr(_tribesArray._array).ToString("X16")}");

        LogHelper.Information($"SizeOf(GameTribeManagerAPI)={Marshal.SizeOf<GameTribeManager>()}, SizeOf(GameTribe)={Marshal.SizeOf<GameTribe>()}");
    }

    /// <summary>
    /// Gets a native pointer to the current game tribe manager instance.
    /// </summary>
    /// <returns>A <see cref="NativePointer{GameTribeManager}"/> representing the game tribe manager.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativePointer<GameTribeManager> GetTribeManager()
    {
        return _tribeManager;
    }

    /// <summary>
    /// Returns the underlying array of game tribes managed by this instance.
    /// </summary>
    /// <returns>A <see cref="SimpleNativeArray{GameTribe}"/> containing all tribes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimpleNativeArray<GameTribe> GetTribeArray()
    {
        return _tribesArray;
    }

    /// <summary>
    /// Returns a span representing the current collection of tribes managed by the instance.
    /// </summary>
    /// <returns>A <see cref="Span{GameTribe}"/> containing the tribes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<GameTribe> GetTribeAsSpan()
    {
        return _tribesArray.AsSpan();
    }

    /// <summary>
    /// Attempts to retrieve a direct, raw pointer to a tribe by its ID.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe to retrieve.</param>
    /// <param name="tribe">When this method returns, contains a pointer to the tribe if found; otherwise, null.</param>
    /// <returns><c>true</c> if the tribe was found and is within the valid array bounds; otherwise, <c>false</c>.</returns>
    public bool TryGetTribeById(int tribeId, out GameTribe* tribe)
    {
        tribe = null;
        if (!IsValidId(tribeId))
        {
            LogHelper.Error($"Tried to access tribe index that was out of range: [{tribeId}/{_tribesArray.Length}]");
            return false;
        }

        if (_tribesArray._array == null)
            return false;

        tribe = &_tribesArray._array[tribeId - 1];
        return true;
    }

    /// <summary>
    /// Attempts to retrieve a safe, wrapped pointer to a tribe by its ID.
    /// </summary>
    /// <param name="id">The ID of the tribe to retrieve.</param>
    /// <param name="tribe">When this method returns, contains a <see cref="NativePointer{GameTribe}"/> wrapping the tribe object if found; otherwise, an invalid pointer.</param>
    /// <returns><c>true</c> if the tribe was found; otherwise, <c>false</c>.</returns>
    public bool TryGetTribeByIdEx(int id, out NativePointer<GameTribe> tribe)
    {
        bool result = TryGetTribeById(id, out GameTribe* tribePtr);
        tribe = new NativePointer<GameTribe>(tribePtr);
        return result;
    }

    /// <summary>
    /// Creates a new, empty tribe.
    /// </summary>
    /// <param name="initialPlayerOwner">The player that initially owns this tribe. Defaults to 0 (neutral/wildlife).</param>
    /// <param name="bUnknown">An unidentified boolean parameter passed to the game's internal function.</param>
    /// <returns>The unique ID of the newly instantiated tribe.</returns>
    [LuaApiExport("Create")]
    public Int64 Create(int initialPlayerOwner = 0, bool bUnknown = false)
    {
        return BulkTribeDetours.c_game_create_new_tribe_hook_impl(_tribeManager, initialPlayerOwner, (byte)(bUnknown ? 1 : 0));
    }

    /// <summary>
    /// Safely marks a tribe object for deletion by the game engine.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe to delete.</param>
    /// <returns><c>true</c> if the tribe was found and marked for deletion; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This is the recommended way to delete tribe. It changes its state, allowing the
    /// game engine to clean it up gracefully on a subsequent frame.
    /// </remarks>
    [LuaApiExport("DeleteSafe")]
    public bool DeleteTribeSafe(int tribeId)
    {
        if (!TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return false;
        }
        tribe->r_AliveState = AliveState.MarkedForDeletion;
        return true;
    }

    /// <summary>
    /// Immediately deletes a tribe object from the game.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe to delete.</param>
    /// <remarks>
    /// Use with caution, as immediate deletion could cause issues if other game systems are
    /// still referencing the object. Prefer <see cref="DeleteTribeSafe"/> where possible.
    /// </remarks>
    [LuaApiExport("Delete")]
    public void DeleteTribe(int tribeId)
    {
        if (!IsValid(tribeId))
        {
            LogHelper.Warning($"Tried to delete invalid entity: {tribeId}");
            return;
        }
        BulkTribeDetours.c_game_tribe_delete_hook_impl(_tribeManager, tribeId);
    }

    /// <summary>
    /// Checks if a tribe id is valid and exists.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe to check.</param>
    /// <returns><c>true</c> if the tribe was found and is valid; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsValid")]
    public bool IsValid(int tribeId)
    {
        if (!IsValidId(tribeId))
            return false;

        if (!TryGetTribeById(tribeId, out GameTribe* tribe))
        {
            LogHelper.Warning($"Could not find tribe by id: {tribeId}");
            return false;
        }
        return (int)tribe->r_AliveState != 0;
    }

    /// <summary>
    /// Checks if a tribe id is valid.
    /// </summary>
    /// <param name="tribeId">The ID to check.</param>
    /// <returns><c>true</c> if the tribe id is valid; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsValidId")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValidId(int tribeId)
    {
        if (tribeId <= 0 || tribeId > _tribesArray.Length)
            return false;

        return true;
    }

    /// <summary>
    /// Spawns a group of units (european or arab, or both)
    /// Used to spawn the starter units on the map internally.
    /// TODO: Document a1, a2
    /// <param name="a1">Undocumented (Something todo with the tribe)</param>
    /// <param name="a2">Undocumented (Something todo with the tribe)</param>
    /// <param name="tileX">Tile X</param>
    /// <param name="tileY">Tile Y</param>
    /// <param name="playerId">The player id to spawn for</param>
    /// <param name="unitType1">European unit type</param>
    /// <param name="unitType2">Arab unit type</param>
    /// <param name="amount1">European unit amount</param>
    /// <param name="amount2">Arab unit amount</param>
    /// </summary>
    [LuaApiExport("CreateWith")]
    public int AssignUnit(int a1, int a2, int tileX, int tileY, int playerId, eChimps unitType1, eChimps unitType2, int amount1, int amount2)
    {
        return (int)BulkTribeDetours.c_game_spawn_unit_group_eu_and_arab_hook_impl(_tribeManager, (UInt64)a1, (UInt64)a2, tileX, tileY, playerId, unitType1, unitType2, amount1, amount2);
    }

    /// <summary>
    /// Gets the combat stance for a tribe.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <returns>The <see cref="TribeStance"/>. Defaults to Defensive if not found.</returns>
    [LuaApiExport("GetStance")]
    public TribeStance GetStance(int tribeId)
    {
        if (!TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return TribeStance.Defensive;
        }
        return tribe->r_TribeStance;
    }

    /// <summary>
    /// Sets the combat stance for a tribe.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <param name="stance">The new <see cref="TribeStance"/> to set.</param>
    /// <returns><c>true</c> if the tribe was found and the stance was set; otherwise, <c>false</c>.</returns>
    [LuaApiExport("SetStance")]
    public bool SetStance(int tribeId, TribeStance stance)
    {
        if (!TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return false;
        }
        tribe->r_TribeStance = stance;
        return true;
    }

    /// <summary>
    /// Gets whether a tribe (animal) will attack the nearest available unit near to it.
    /// Usually triggered by attacking lions.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <returns>The state. Defaults to false if not found.</returns>
    [LuaApiExport("GetAttackNearestUnit")]
    public bool GetAttackNearestUnit(int tribeId)
    {
        if (!TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return false;
        }
        return tribe->r_bAttackNearestUnit == 1;
    }

    /// <summary>
    /// Sets whether a tribe (animal) will attack the nearest available unit near to it.
    /// Usually triggered by attacking lions.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <param name="state">The new state to set.</param>
    /// <returns><c>true</c> if the tribe was found and the state was set; otherwise, <c>false</c>.</returns>
    [LuaApiExport("SetAttackNearestUnit")]
    public bool SetAttackNearestUnit(int tribeId, bool state)
    {
        if (!TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return false;
        }
        tribe->r_bAttackNearestUnit = state ? (UInt16)1 : (UInt16)0;
        return true;
    }

    /// <summary>
    /// Assigns a unit to a tribe.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe to assign the unit to.</param>
    /// <param name="unitId">The ID of the unit to assign.</param>
    /// <returns><c>true</c> if the tribe was found and the assignment was issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("AddUnit")]
    public bool AssignUnit(int tribeId, int unitId)
    {
        LogHelper.Debug($"tribeId={tribeId}, unitId={unitId}");
        if (!TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return false;
        }
        BulkTribeDetours.c_game_unit_assign_tribe_hook_impl(_tribeManager, unitId, tribeId);

        return true;
    }

    /// <summary>
    /// Manually unassigns a unit from a tribe (custom implementation)
    /// Use the normal variant where ever you can.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <param name="unitId">The ID of the unit to unassign.</param>
    /// <returns><c>true</c> if both tribe and unit were found and the unit was unassigned; otherwise, <c>false</c>.</returns>
    [LuaApiExport("RemoveUnit")]
    public bool UnassignUnit(int tribeId, int unitId)
    {
        if (!TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return false;
        }
        if (GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) == false || unit == null)
        {
            LogHelper.Error($"Could not find unit with id: {unitId}");
            return false;
        }
        BulkTribeDetours.c_game_tribe_remove_unit_hook_impl(_tribeManager, unitId, tribeId);
        return true;
    }

    /// <summary>
    /// Manually unassigns a unit from a tribe (custom implementation)
    /// Use the normal variant where ever you can.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <param name="unitId">The ID of the unit to unassign.</param>
    /// <param name="reassignNewTribeLeader">If true and this unit was the former team-leader, this function will re-assign the tribe a new one.</param>
    /// <returns><c>true</c> if both tribe and unit were found and the unit was unassigned; otherwise, <c>false</c>.</returns>
    [LuaApiExport("RemoveUnitEx")]
    public bool UnassignUnitEx(int tribeId, int unitId, bool reassignNewTribeLeader = false)
    {
        if (!TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return false;
        }
        if (GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) == false || unit == null)
        {
            LogHelper.Error($"Could not find unit with id: {unitId}");
            return false;
        }
        tribe->r_UnitsInGroup--;

        if (tribe->r_LeaderUnitId == unitId)
        {
            tribe->r_LeaderUnitId = 0;

            if (reassignNewTribeLeader)
                ReassignTribeLeader((int)unit->r_GlobalId, tribeId, tribe);
        }

        unit->r_TribeId = 0;
        unit->r_TribeLeaderUnitId = 0;

        return true;
    }

    /// <summary>
    /// Tries to reassign a tribe a new leader automatically (first unit found from the former tribe)
    /// </summary>
    /// <param name="excludeUnitGlobalId">A unit global id to exclude</param>
    /// <param name="tribeId">The tribeId to set a new leader for</param>
    /// <param name="tribe">The tribe to set a new leader for </param>
    /// <returns>Returns <c>true</c> on successful assign; Otherwise <c>false</c></returns>
    private bool ReassignTribeLeader(int excludeUnitGlobalId, int tribeId, GameTribe* tribe)
    {
        List<int> unitsIdsOfTribe = new List<int>();

        GameUnitManagerAPI.Instance.QueryUnits().Where((in GameUnit x) => x.r_GlobalId != excludeUnitGlobalId && x.r_TribeId == tribeId).ToIdList(unitsIdsOfTribe);
        if (unitsIdsOfTribe.Count <= 0)
        {
            return false;
        }
        tribe->r_LeaderUnitId = (ushort)unitsIdsOfTribe.First();
        return true;
    }

    /// <summary>
    /// Adds a unit to the tribe's list of tracked ranged attackers.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <param name="unitId">The ID of the ranged unit attacking this tribe.</param>
    /// <returns><c>true</c> if the tribe and unit were found and the tracker was added; otherwise, <c>false</c>.</returns>
    [LuaApiExport("AddRangedAttackerToTracker")]
    public bool AddRangedAttackerToTracker(int tribeId, int unitId)
    {
        if (!TryGetTribeByIdEx(tribeId, out NativePointer<GameTribe> tribe) || tribe == IntPtr.Zero)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return false;
        }

        if (GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) == false || unit == null)
        {
            LogHelper.Error($"Could not find unit with id: {unitId}");
            return false;
        }

        int index = tribe.Pointer->r_TrackedRangedAttackersCount;
        tribe.SetTrackedRangedAttackerIndex(index, (UInt16)unit->r_GlobalId);
        tribe.Pointer->r_TrackedRangedAttackersCount = (UInt16)Math.Min(index + 1, 10);
        return true;
    }

    /// <summary>
    /// Sets a patrol path for a tribe.
    /// Custom impl.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <param name="path">An array of up to 10 tile coordinates that define the patrol path.</param>
    /// <param name="patrolMode">The mode of patrol (e.g., infinite loop, one-shot).</param>
    /// <param name="startingPatrolPointIndex">The index in the path array where the tribe should start its patrol.</param>
    /// <param name="moveType">The movement type the tribe should use while patrolling.</param>
    /// <returns><c>true</c> if the path was successfully set; otherwise, <c>false</c>.</returns>
    public bool SetPatrolPath(int tribeId, UnmanagedVector2<UInt16>[] path, TribePatrolMode patrolMode = TribePatrolMode.PatrolInfinite, int startingPatrolPointIndex = 0, TribeMoveType moveType = TribeMoveType.DefaultInSync)
    {
        if (!TryGetTribeByIdEx(tribeId, out NativePointer<GameTribe> tribe) || tribe == IntPtr.Zero)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return false;
        }
        int len = path.Length;
        if (len > 10)
        {
            LogHelper.Error($"Patrol path length {len} exceeds max length {10}");
            return false;
        }

        if (startingPatrolPointIndex < 0 || startingPatrolPointIndex >= len)
        {
            LogHelper.Error($"startingPatrolPointIndex {startingPatrolPointIndex} is out of range for path length {len}");
            return false;
        }

        UnmanagedVector2<UInt16> startingPoint = path[startingPatrolPointIndex];
        IssueMoveHereCommand(tribeId, startingPoint.X, startingPoint.Y, true, 1, moveType);

        for (int i = 0; i < len; i++)
            tribe.SetPatrolPoint(i, path[i]);

        tribe.Pointer->r_PatrolMode = patrolMode;
        tribe.Pointer->r_PatrolCurrentTargetIndex = (UInt16)startingPatrolPointIndex;
        tribe.Pointer->r_CurrentPatrolPoints = (byte)len;

        return true;
    }

    /// <summary>
    /// Gets a patrol path of a tribe.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <returns>The current patrol path of a tribe; Otherwise empty collection.</returns>
    [LuaApiExport("GetPatrolPath")]
    public UnmanagedVector2<UInt16>[] GetPatrolPath(int tribeId)
    {
        if (!TryGetTribeByIdEx(tribeId, out NativePointer<GameTribe> tribe) || tribe == IntPtr.Zero)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return [];
        }

        int len = (int)tribe.Pointer->r_CurrentPatrolPoints;
        if (len == 0)
        {
            return [];
        }

        List<UnmanagedVector2<UInt16>> points = new List<UnmanagedVector2<ushort>>();
        for (int i = 0; i < len; i++)
            points.Add(tribe.GetPatrolPoint(i));

        return [.. points];
    }

    /// <summary>
    /// Gets the current target waypoint for a tribe on its existing patrol path.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <returns>The current patrol waypoint index; Otherwise -1.</returns>
    [LuaApiExport("GetCurrentPatrolIndex")]
    public int GetCurrentPatrolPointIndex(int tribeId)
    {
        if (!TryGetTribeByIdEx(tribeId, out NativePointer<GameTribe> tribe) || tribe == IntPtr.Zero)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return -1;
        }
        return tribe.Pointer->r_PatrolCurrentTargetIndex;
    }

    /// <summary>
    /// Sets the current target waypoint for a tribe on its existing patrol path.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <param name="patrolPointIndex">The index of the patrol point to set as the current target.</param>
    /// <returns><c>true</c> if the index was valid and set; otherwise, <c>false</c>.</returns>
    [LuaApiExport("SetCurrentPatrolIndex")]
    public bool SetCurrentPatrolPointIndex(int tribeId, int patrolPointIndex)
    {
        if (!TryGetTribeByIdEx(tribeId, out NativePointer<GameTribe> tribe) || tribe == IntPtr.Zero)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return false;
        }
        if (patrolPointIndex < 0 || patrolPointIndex >= tribe.Pointer->r_CurrentPatrolPoints)
        {
            LogHelper.Error($"patrolPointIndex {patrolPointIndex} is out of range for current patrol points {tribe.Pointer->r_CurrentPatrolPoints}");
            return false;
        }
        tribe.Pointer->r_PatrolCurrentTargetIndex = (UInt16)patrolPointIndex;
        return true;
    }

    /// <summary>
    /// Clears the patrol path for a tribe, stopping its patrol behavior.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <returns><c>true</c> if the tribe was found and its path was cleared; otherwise, <c>false</c>.</returns>
    [LuaApiExport("ClearPatrolPath")]
    public bool ClearPatrolPath(int tribeId)
    {
        if (!TryGetTribeByIdEx(tribeId, out NativePointer<GameTribe> tribe) || tribe == IntPtr.Zero)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return false;
        }

        UnmanagedVector2<UInt16> zero = new UnmanagedVector2<UInt16>() { X = 0, Y = 0 };
        for (int i = 0; i < 10; i++)
            tribe.SetPatrolPoint(i, zero);

        tribe.Pointer->r_CurrentPatrolPoints = 0;
        tribe.Pointer->r_PatrolCurrentTargetIndex = 0;
        tribe.Pointer->r_PatrolMode = TribePatrolMode.None;
        return true;
    }

    /// <summary>
    /// Retrieves all unit IDs belonging to the specified tribe by scanning its membership bitfield.
    /// </summary>
    /// <remarks>
    /// The tribe stores its members in a flat bitset of 625 consecutive <see cref="UInt16"/> words
    /// (10,000 bits total), where each set bit's absolute position across the array directly encodes
    /// a unit ID. Word <c>n</c> covers unit IDs <c>n*16</c> through <c>n*16+15</c>.
    /// <para>
    /// Scanning stops early once <see cref="GameTribe.r_UnitsInGroup"/> members have been found,
    /// avoiding a full 625-word traversal for small tribes.
    /// </para>
    /// </remarks>
    /// <param name="tribeId">The ID of the tribe whose units should be retrieved.</param>
    /// <param name="results">
    /// A caller-provided list that will be populated with the unit IDs of all members of the tribe.
    /// The list is not cleared before population; existing entries are preserved.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the tribe was found and the results list was successfully populated;
    /// <see langword="false"/> if no tribe with the given <paramref name="tribeId"/> could be located.
    /// </returns>
    /// <example>
    /// <code>
    /// var unitIds = new List&lt;int&gt;();
    /// if (GetUnits(myTribeId, unitIds))
    /// {
    ///     foreach (int unitId in unitIds)
    ///         Console.WriteLine($"Tribe {myTribeId} contains unit {unitId}");
    /// }
    /// </code>
    /// </example>
    public bool GetUnits(int tribeId, List<int> results)
    {
        if (!TryGetTribeByIdEx(tribeId, out NativePointer<GameTribe> tribe) || tribe == IntPtr.Zero)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return false;
        }

        UInt16* bitmapStart = &tribe.Pointer->r_UnitIdsInGroupBitfield;
        for (int wordIdx = 0; wordIdx < 625; wordIdx++)
        {
            UInt16 word = bitmapStart[wordIdx];
            if (word == 0) 
                continue;

            for (int bit = 0; bit < 16; bit++)
            {
                if ((word & (1 << bit)) != 0)
                {
                    int unitId = wordIdx * 16 + bit;
                    results.Add(unitId);

                    // Early out once we found all members
                    if (results.Count >= tribe.Pointer->r_UnitsInGroup)
                        return true;
                }
            }
        }
        return true;
    }

    //
    // AI Order Functions
    //

    /// <summary>
    /// Issues a low-level, target-based command to a tribe.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe receiving the command.</param>
    /// <param name="command">The <see cref="TribeAICommand"/> to issue.</param>
    /// <param name="arg1">The first command argument (often a target ID or X-coordinate).</param>
    /// <param name="arg2">The second command argument (often a global ID or Y-coordinate).</param>
    /// <param name="arg3">The third command argument.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IssueCommand")]
    public bool IssueTargettedCommand(int tribeId, TribeAICommand command, int arg1, int arg2, int arg3 = 0)
    {
        if (!TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return false;
        }
        Int64 result = BulkTribeDetours.c_game_tribe_issueorder_withtarget_hook_impl(_tribeManager, tribeId, command, arg1, arg2, arg3);
        if (result != 1)
        {
            LogHelper.Error($"Failed to issue command {command} to tribe {tribeId} with args {arg1}, {arg2}, {arg3}. Result: {result}");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Issues a "move to location" command to a tribe.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <param name="tileX">The destination tile X-coordinate.</param>
    /// <param name="tileY">The destination tile Y-coordinate.</param>
    /// <param name="isPatrolPath">Whether this movement is part of a patrol.</param>
    /// <param name="bIsNewOrder">Should the game consider this a brand new order.</param>
    /// <param name="tribeMoveType">The type of movement formation.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IssueMoveCommand")]
    public bool IssueMoveHereCommand(int tribeId, int tileX, int tileY, bool isPatrolPath = false, int bIsNewOrder = 1, TribeMoveType tribeMoveType = TribeMoveType.DefaultInSync)
    {
        if (!TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return false;
        }
        Int64 result = BulkTribeDetours.c_game_tribe_issueorder_movehere_hook_impl(_tribeManager, tribeId, tileX, tileY, (short)(isPatrolPath ? 1 : 0), bIsNewOrder, tribeMoveType);
        if (result != 1)
        {
            LogHelper.Error($"Failed to issue move command to tribe {tribeId} to position ({tileX}, {tileY}). Result: {result}");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Attempts to retrieve the global identifier for a tribe.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <param name="globalId">When this method returns, contains the global ID if the tribe was found.</param>
    /// <returns><c>true</c> if the tribe was found and its global ID was retrieved; otherwise, <c>false</c>.</returns>
    public bool TryGetGlobalId(int tribeId, out uint globalId)
    {
        globalId = 0;
        if (!TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return false;
        }
        globalId = tribe->r_GlobalId;
        return true;
    }

    /// <summary>
    /// Get a tribe by globalId
    /// </summary>
    /// <param name="globalId">The tribe global id.</param>
    /// <returns>The global id; Otherwise -1</returns>
    [LuaApiExport("GetByGlobalId")]
    public int GetByGlobalId(int globalId)
    {
        List<int> results = new List<int>();
        QueryTribes().Where(TribePredicates.HasGlobalId(globalId)).ToIdList(results);
        if (results.Count == 0)
            return -1;
        return results[0];
    }

    /// <summary>
    /// Attempts to retrieve the leader unit id of the tribe.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe.</param>
    /// <returns>The leader unit id; Otherwise -1</returns>
    [LuaApiExport("GetLeaderUnitId")]
    public int GetLeaderUnitId(int tribeId)
    {
        if (!TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
        {
            LogHelper.Error($"Could not find tribe with id: {tribeId}");
            return -1;
        }
        return tribe->r_LeaderUnitId;
    }

    //
    // AI Order Function Wrappers: quality-of-life versions without globalId provided
    //

    /// <summary>
    /// Orders a tribe to attack a specific unit.
    /// </summary>
    [LuaApiExport("Order_AttackUnit")]
    public bool AttackUnit(int tribeId, int targetUnitId)
    {
        int globalId = GameUnitManagerAPI.Instance.GetGlobalId(targetUnitId);
        if (globalId == -1)
        {
            LogHelper.Error($"Could not find globalid for unit with id: {targetUnitId}");
            return false;
        }
        return AttackUnitEx(tribeId, targetUnitId, globalId);
    }

    /// <summary>
    /// Orders a tribe to attack a specific building.
    /// </summary>
    /// <param name="tribeId">The ID of the attacking tribe.</param>
    /// <param name="targetBuildingId">The ID of the building to attack.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_AttackBuilding")]
    public bool AttackBuilding(int tribeId, int targetBuildingId)
    {
        int globalId = GameBuildingManagerAPI.Instance.GetGlobalId(targetBuildingId);
        if (globalId == -1)
        {
            LogHelper.Error($"Could not find globalid for building with id: {targetBuildingId}");
            return false;
        }
        return AttackBuildingEx(tribeId, targetBuildingId, globalId);
    }

    /// <summary>
    /// Orders engineers to man a pitch cauldron or build a siege tent at a building's location.
    /// </summary>
    /// <param name="tribeId">The ID of the engineer tribe.</param>
    /// <param name="targetBuildingId">The ID of the target building (e.g., a Gatehouse for a pitch cauldron).</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_ManPitchCauldronOrBuildTent")]
    public bool ManPitchCauldronOrBuildTent(int tribeId, int targetBuildingId)
    {
        int globalId = GameBuildingManagerAPI.Instance.GetGlobalId(targetBuildingId);
        if (globalId == -1)
        {
            LogHelper.Error($"Could not find globalid for building with id: {targetBuildingId}");
            return false;
        }
        return ManPitchCauldronOrBuildTentEx(tribeId, targetBuildingId, globalId);
    }

    /// <summary>
    /// Orders a tribe of engineers to man a piece of siege equipment.
    /// </summary>
    /// <param name="tribeId">The ID of the engineer tribe.</param>
    /// <param name="targetUnitId">The ID of the siege equipment unit to man.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_ManSiegeEquipment")]
    public bool ManSiegeEquipment(int tribeId, int targetUnitId)
    {
        int globalId = GameUnitManagerAPI.Instance.GetGlobalId(targetUnitId);
        if (globalId == -1)
        {
            LogHelper.Error($"Could not find globalid for unit with id: {targetUnitId}");
            return false;
        }
        return ManSiegeEquipmentEx(tribeId, targetUnitId, globalId);
    }

    /// <summary>
    /// Orders a tribe of engineers to disassemble their manned siege equipment.
    /// </summary>
    /// <param name="tribeId">The ID of the engineer tribe.</param>
    /// <param name="targetUnitId">The ID of the siege equipment unit to dissolve.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_DissolveSiegeEquipment")]
    public bool DissolveSiegeEquipment(int tribeId, int targetUnitId)
    {
        int globalId = GameUnitManagerAPI.Instance.GetGlobalId(targetUnitId);
        if (globalId == -1)
        {
            LogHelper.Error($"Could not find globalid for unit with id: {targetUnitId}");
            return false;
        }
        return DissolveSiegeEquipmentEx(tribeId, targetUnitId, globalId);
    }

    /// <summary>
    /// Orders a tribe of tunnelers to dig a tunnel
    /// </summary>
    /// <param name="tribeId">The ID of the tunneler tribe.</param>
    /// <param name="targetBuildingId">The ID of the tunnel building.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_BuildTunnel")]
    public bool BuildTunnel(int tribeId, int targetBuildingId)
    {
        int globalId = GameBuildingManagerAPI.Instance.GetGlobalId(targetBuildingId);
        if (globalId == -1)
        {
            LogHelper.Error($"Could not find globalid for building with id: {targetBuildingId}");
            return false;
        }
        return BuildTunnelEx(tribeId, targetBuildingId, globalId);
    }

    /// <summary>
    /// Orders a tribe to attack a building.
    /// </summary>
    /// <param name="tribeId">The ID of the attacking tribe.</param>
    /// <param name="targetBuildingId">The ID of the building to attack.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_ForceAttackBuilding")]
    public bool ForceAttackBuilding(int tribeId, int targetBuildingId)
    {
        int globalId = GameBuildingManagerAPI.Instance.GetGlobalId(targetBuildingId);
        if (globalId == -1)
        {
            LogHelper.Error($"Could not find globalid for building with id: {targetBuildingId}");
            return false;
        }
        return ForceAttackBuildingEx(tribeId, targetBuildingId, globalId);
    }

    //
    // AI Order Function Wrappers: Extended versions with globalId provided
    //

    /// <summary>
    /// Orders a tribe to move to a specific tile coordinate.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe to command.</param>
    /// <param name="tileX">The destination tile X-coordinate.</param>
    /// <param name="tileY">The destination tile Y-coordinate.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_MoveTo")]
    public bool MoveTo(int tribeId, int tileX, int tileY) => IssueMoveHereCommand(tribeId, tileX, tileY);

    /// <summary>
    /// Orders a tribe to attack a specific unit, providing the target's global ID.
    /// </summary>
    /// <param name="tribeId">The ID of the attacking tribe.</param>
    /// <param name="targetUnitId">The ID of the unit to attack.</param>
    /// <param name="targetGlobalId">The global ID of the target unit.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_AttackUnitEx")]
    public bool AttackUnitEx(int tribeId, int targetUnitId, int targetGlobalId) => IssueTargettedCommand(tribeId, TribeAICommand.AttackUnit, targetUnitId, targetGlobalId);

    /// <summary>
    /// Orders a tribe to attack a specific tile on the map.
    /// </summary>
    /// <param name="tribeId">The ID of the attacking tribe.</param>
    /// <param name="targetTileX">The X-coordinate of the target tile.</param>
    /// <param name="targetTileY">The Y-coordinate of the target tile.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_AttackTile")]
    public bool AttackTile(int tribeId, int targetTileX, int targetTileY) => IssueTargettedCommand(tribeId, TribeAICommand.AttackTilePosition, targetTileX, targetTileY);

    /// <summary>
    /// Orders a tribe to dig a moat at a specific tile.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe (typically Spearmen).</param>
    /// <param name="targetTileX">The X-coordinate of the target tile.</param>
    /// <param name="targetTileY">The Y-coordinate of the target tile.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_DigMoat")]
    public bool DigMoat(int tribeId, int targetTileX, int targetTileY) => IssueTargettedCommand(tribeId, TribeAICommand.DigMoatTileId, targetTileX, targetTileY, 1000);

    /// <summary>
    /// Orders a tribe to attack a specific building, providing the target's global ID.
    /// </summary>
    /// <param name="tribeId">The ID of the attacking tribe.</param>
    /// <param name="targetBuildingId">The ID of the building to attack.</param>
    /// <param name="targetGlobalId">The global ID of the target building.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_AttackBuildingEx")]
    public bool AttackBuildingEx(int tribeId, int targetBuildingId, int targetGlobalId) => IssueTargettedCommand(tribeId, TribeAICommand.AttackBuilding, targetBuildingId, targetGlobalId);

    /// <summary>
    /// Orders engineers to man a pitch cauldron or build a siege tent, providing the target's global ID.
    /// </summary>
    /// <param name="tribeId">The ID of the engineer tribe.</param>
    /// <param name="targetBuildingId">The ID of the target building.</param>
    /// <param name="targetGlobalId">The global ID of the target building.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_ManPitchCauldronOrBuildTentEx")]
    public bool ManPitchCauldronOrBuildTentEx(int tribeId, int targetBuildingId, int targetGlobalId) => IssueTargettedCommand(tribeId, TribeAICommand.ManPitchCauldronOrBuildTent, targetBuildingId, targetGlobalId);

    /// <summary>
    /// Orders a tribe of engineers to man siege equipment, providing the target's global ID.
    /// </summary>
    /// <param name="tribeId">The ID of the engineer tribe.</param>
    /// <param name="targetUnitId">The ID of the siege equipment unit to man.</param>
    /// <param name="targetGlobalId">The global ID of the siege equipment unit.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_ManSiegeEquipmentEx")]
    public bool ManSiegeEquipmentEx(int tribeId, int targetUnitId, int targetGlobalId) => IssueTargettedCommand(tribeId, TribeAICommand.ManSiegeEquipment, targetUnitId, targetGlobalId);

    /// <summary>
    /// Orders a tribe of engineers to disassemble their manned siege equipment, providing the target's global ID.
    /// </summary>
    /// <param name="tribeId">The ID of the engineer tribe.</param>
    /// <param name="targetUnitId">The ID of the siege equipment unit to dissolve.</param>
    /// <param name="targetGlobalId">The global ID of the siege equipment unit.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_DissolveSiegeEquipmentEx")]
    public bool DissolveSiegeEquipmentEx(int tribeId, int targetUnitId, int targetGlobalId) => IssueTargettedCommand(tribeId, TribeAICommand.DissolveSiegeEquipment, targetUnitId, targetGlobalId, 0);

    /// <summary>
    /// Orders a tribe of engineers to throw lava at a specific tile.
    /// </summary>
    /// <param name="tribeId">The ID of the engineer tribe.</param>
    /// <param name="targetTileX">The X-coordinate of the target tile.</param>
    /// <param name="targetTileY">The Y-coordinate of the target tile.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_ThrowLava")]
    public bool ThrowLava(int tribeId, int targetTileX, int targetTileY) => IssueTargettedCommand(tribeId, TribeAICommand.ThrowLava, targetTileX, targetTileY);

    /// <summary>
    /// Orders a tribe of tunnelers to dig a tunnel towards a building, providing the target's global ID.
    /// </summary>
    /// <param name="tribeId">The ID of the tunneler tribe.</param>
    /// <param name="targetBuildingId">The ID of the target building.</param>
    /// <param name="targetGlobalId">The global ID of the target building.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_BuildTunnelEx")]
    public bool BuildTunnelEx(int tribeId, int targetBuildingId, int targetGlobalId) => IssueTargettedCommand(tribeId, TribeAICommand.BuildTunnel, targetBuildingId, targetGlobalId, 0);

    /// <summary>
    /// Orders a tribe to attack a wall at a specific tile ID.
    /// </summary>
    /// <param name="tribeId">The ID of the attacking tribe.</param>
    /// <param name="targetTileId">The tile ID of the wall segment to attack.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_AttackWall")]
    public bool AttackWall(int tribeId, int targetTileId) => IssueTargettedCommand(tribeId, TribeAICommand.AttackWallTileId, targetTileId, 0);

    /// <summary>
    /// Orders a tribe of laddermen to place a ladder on a wall.
    /// </summary>
    /// <param name="tribeId">The ID of the ladderman tribe.</param>
    /// <param name="targetTileId">The tile ID of the wall segment to place the ladder on.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_AttachLadderToWall")]
    public bool AttachLadderToWall(int tribeId, int targetTileId) => IssueTargettedCommand(tribeId, TribeAICommand.AttachLadderToWall, targetTileId, 0);


    /// <summary>
    /// Orders the units in a tribe to disband and return to being civilians at the keep.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe to dissolve.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_Dissolve")]
    public bool Dissolve(int tribeId) => IssueTargettedCommand(tribeId, TribeAICommand.UnitDissolve, 0, 0, 1);

    /// <summary>
    /// Orders a tribe to stop its current action and go idle.
    /// </summary>
    /// <param name="tribeId">The ID of the tribe to stop.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_Stop")]
    public bool Stop(int tribeId) => IssueTargettedCommand(tribeId, TribeAICommand.UnitStop, 0, 0, 1);

    /// <summary>
    /// Orders a tribe to attack a building, and providing the target's global ID.
    /// </summary>
    /// <param name="tribeId">The ID of the attacking tribe.</param>
    /// <param name="targetBuildingId">The ID of the building to attack.</param>
    /// <param name="targetGlobalId">The global ID of the target building.</param>
    /// <returns><c>true</c> if the command was successfully issued; otherwise, <c>false</c>.</returns>
    [LuaApiExport("Order_ForceAttackBuildingEx")]
    public bool ForceAttackBuildingEx(int tribeId, int targetBuildingId, int targetGlobalId) => IssueTargettedCommand(tribeId, TribeAICommand.ForceAttackBuilding, targetBuildingId, targetGlobalId, -127);


    #region Query System

    /// <summary>
    /// Begins a high-performance query over all possible unit slots.
    /// </summary>
    public GameStructQuery<GameTribe> QueryTribes()
    {
        return new GameStructQuery<GameTribe>(_tribesArray._array, _tribesArray.Length);
    }

    /// <summary>
    /// The core query execution method. All public query functions delegate to this.
    /// </summary>
    public void ExecuteQuery(List<int> results, RefPredicate<GameTribe> basePredicate, AliveState? stateFilter, PlayerRelationship? relationship, int? povPlayerId)
    {
        GameStructQuery<GameTribe> query = QueryTribes().Where(basePredicate);

        if (stateFilter.HasValue)
        {
            AliveState state = stateFilter.Value;
            query = query.Where((in unit) => unit.r_AliveState == state);
        }

        if (relationship != PlayerRelationship.Any && povPlayerId.HasValue)
        {
            switch (relationship)
            {
                case PlayerRelationship.Allied:
                    query = query.Where(TribePredicates.IsAlliedTo(povPlayerId.Value));
                    break;
                case PlayerRelationship.Enemy:
                    query = query.Where(TribePredicates.IsEnemyTo(povPlayerId.Value));
                    break;
                case PlayerRelationship.Self:
                    query = query.Where(TribePredicates.IsOwnedBy(povPlayerId.Value));
                    break;
            }
        }

        query.ToIdList(results);
    }

    /// <summary>
    /// A set of common, reusable predicates for convenience.
    /// </summary>
    public static class TribePredicates
    {
        public static readonly RefPredicate<GameTribe> IsAlive = static (in unit) => unit.r_AliveState == AliveState.IsAlive;
        public static readonly RefPredicate<GameTribe> IsDead = static (in unit) => unit.r_AliveState == AliveState.MarkedForDeletion;

        // Predicate to match any unit, used as a default for generic queries.
        public static readonly RefPredicate<GameTribe> Any = static (in unit) => true;

        public static RefPredicate<GameTribe> IsOwnedBy(int playerOwnerId) => (in unit) => unit.r_PlayerIdOwner == playerOwnerId;
        public static RefPredicate<GameTribe> IsAlliedTo(int playerId) => (in unit) => GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(unit.r_PlayerIdOwner, playerId);
        public static RefPredicate<GameTribe> IsEnemyTo(int playerId) => (in unit) => !GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(unit.r_PlayerIdOwner, playerId);
        public static RefPredicate<GameTribe> HasGlobalId(int globalId) => (in unit) => unit.r_GlobalId == globalId;
    }

    /// <summary>
    /// Fills a list with IDs of all tribes, with optional filters (no spatial constraint).
    /// </summary>
    public void GetAllTribes(List<int> results, AliveState? stateFilter = null, PlayerRelationship? relationship = PlayerRelationship.Any, int? povPlayerId = 1)
    {
        ExecuteQuery(results, TribePredicates.Any, stateFilter, relationship, povPlayerId);
    }

    #endregion


}

