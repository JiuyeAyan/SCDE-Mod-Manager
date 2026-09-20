using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// Simple state enum describing the state of a pitch trap.
/// </summary>
public enum PitchState : Int16
{
    /// <summary>
    /// Unidentified
    /// </summary>
    None = 0,

    /// <summary>
    /// All pitch tiles are initially dormant like this.
    /// In this state, they will only react to fire.
    /// </summary>
    Dormant = 1,

    /// <summary>
    /// When this state is set, this pitch trap will be set on fire.
    /// </summary>
    OnFire = 2
}
