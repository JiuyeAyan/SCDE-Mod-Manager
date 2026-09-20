using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// Defines the food ration levels for a player's population.
/// </summary>
public enum RationsMode : UInt32
{
    /// <summary>No food is being distributed.</summary>
    None = 0,
    /// <summary>Half the standard amount of food is distributed.</summary>
    Half = 1,
    /// <summary>The standard amount of food is distributed.</summary>
    Full = 2,
    /// <summary>An extra amount of food is distributed.</summary>
    Extra = 3,
    /// <summary>Double the standard amount of food is distributed.</summary>
    Double = 4
}