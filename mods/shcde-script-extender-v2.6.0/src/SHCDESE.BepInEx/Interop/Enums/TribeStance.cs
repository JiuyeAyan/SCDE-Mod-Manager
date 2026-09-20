using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// Specifies the stance or behavior mode for a tribe, indicating its approach to engagement or defense.
/// </summary>
/// <remarks>Use this enumeration to set or evaluate a tribe's strategic posture. The values represent distinct
/// modes: Hold for maintaining position, Defensive for prioritizing defense of the current location, and Aggressive for prioritizing
/// attack, which causes the unit to follow any units until killed with a lot of autonomy.</remarks>
public enum TribeStance : UInt16
{
    /// <summary>Units will hold their current position and only attack enemies that come into range.</summary>
    Hold = 0,
    /// <summary>Units will defend a small area around their current position, pursuing nearby enemies but returning afterward.</summary>
    Defensive = 1,
    /// <summary>Units will pursue any enemy they see until either the enemy or they are defeated.</summary>
    Aggressive = 2
}