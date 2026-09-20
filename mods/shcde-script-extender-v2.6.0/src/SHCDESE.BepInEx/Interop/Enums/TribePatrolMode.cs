using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// Defines the behavior of a tribe when it reaches the end of its patrol path.
/// </summary>
public enum TribePatrolMode : Int16
{
    /// <summary>The tribe is not patrolling.</summary>
    None = 0,
    /// <summary>The tribe will walk the patrol path once and then stop at the last waypoint.</summary>
    PatrolOnce = -1,
    /// <summary>The tribe will loop the patrol path indefinitely.</summary>
    PatrolInfinite = 1
}