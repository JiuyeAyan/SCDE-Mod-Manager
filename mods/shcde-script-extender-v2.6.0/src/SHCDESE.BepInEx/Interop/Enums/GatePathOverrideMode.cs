using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// c_game_pathfinding_override_all_gate_passability temporarily saves each relevant gate's live state into GameBuilding.r_AIWalkableState
/// and forces its state byte at +0x2A2 to either 2 or 0, updates the gate's edge bits, and later restores the saved state.
/// </summary>
public enum GatePathOverrideMode : UInt16
{
    RestoreSaved = 0,
    ForceClosed = 1,
    ForceOpen = 2
}
