using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// All currently known "states" of entities.
/// Encompassing chimps, buildings, tribes, etc.
/// </summary>
public enum AliveState : Int16
{
    None = 0,

    /// <summary>
    /// Most objects get set with this value right after creation, signalling (apparently) that they need to be initialized still.
    /// </summary>
    NeedsInit = 1,

    /// <summary>
    /// Most objects that are active and working properly are in this state.
    /// </summary>
    IsAlive = 2,

    /// <summary>
    /// Any and all objects marked with this will be deleted shortly (their entire structure area will be zeroed)
    /// </summary>
    MarkedForDeletion = 3,
    Unknown = 4,
    Unknown5 = 5,

    /// <summary>
    /// Unknown if intended, but if anything above this value is set the unit gets essentially frozen. Possibly
    /// undefined behaviour, but it works if you want to "freeze" or "pause" a unit.
    /// </summary>
    Paused = 6
}