using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// Specifies the movement behavior for units within a tribe.
/// </summary>
/// <remarks>Use this enumeration to control whether a unit moves in coordination with the tribe or independently.
/// The values represent distinct movement strategies: 'DefaultInSync' attempts to match the walking speed of other
/// units, while 'Fast' moves at full speed regardless of the tribe's pace. 'Unknown' indicates that the movement type
/// has not been specified.</remarks>
public enum TribeMoveType : Int32
{
    /// <summary>Unit will not change its current move type.</summary>
    NoChange = 0,
    /// <summary>Units will attempt to move in formation, matching the speed of the slowest unit in the tribe.</summary>
    DefaultInSync = 1,
    /// <summary>Units will move at their maximum possible speed, ignoring the formation and speed of other units in the tribe.</summary>
    Fast = -255
}