using SHCDESE.EventAPI.Tribes;
using SHCDESE.Lua.DocsGen;

namespace SHCDESE.EventAPI;

/// <summary>
/// Provides static R3EventHook fields for subscribing to events related to unit groups (tribes).
/// </summary>
public static class TribeR3EventHooks
{
    /// <summary>
    /// Fired when a "move to location" order is issued to a tribe.
    /// </summary>
    [LuaApiExport("OnTribeIssueOrderMoveHere")]
    public static readonly R3EventHook<TribeIssueOrderMoveHereEventArgs> OnTribeIssueOrderMoveHere = new();

    /// <summary>
    /// Fired when a generic, target-based order (e.g., attack unit, attack building) is issued to a tribe.
    /// </summary>
    [LuaApiExport("OnTribeIssueOrderWithTarget")]
    public static readonly R3EventHook<TribeIssueOrderWithTargetEventArgs> OnTribeIssueOrderWithTarget = new();

    /// <summary>
    /// Fired when a unit is assigned to a tribe.
    /// </summary>
    [LuaApiExport("OnTribeAssignUnit")]
    public static readonly R3EventHook<TribeAssignUnitEventArgs> OnTribeAssignUnit = new();

    /// <summary>
    /// Fired when a new tribe is created in the game. The tribe ID is available in the 'Post' phase via the ReturnValue.
    /// </summary>
    [LuaApiExport("OnTribeCreate")]
    public static readonly R3EventHook<TribeCreateEventArgs> OnTribeCreate = new();

    /// <summary>
    /// Fired when the game logic calculates the next waypoint for a patrolling tribe.
    /// </summary>
    [LuaApiExport("OnTribeGetNextPatrolWaypoint")]
    public static readonly R3EventHook<TribeGetNextPatrolWaypointEventArgs> OnTribeGetNextPatrolWaypoint = new();

    /// <summary>
    /// Fired when a tribe is deleted from the game using the low-level deletion function.
    /// </summary>
    [LuaApiExport("OnTribeDelete")]
    public static readonly R3EventHook<TribeDeleteEventArgs> OnTribeDelete = new();
}