using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// Defines the relationship of a unit to a point-of-view player.
/// NOT used by the game. This is a pure script extender enum.
/// </summary>
public enum PlayerRelationship
{
    /// <summary> No relationship filter will be applied. </summary>
    Any,
    /// <summary> The unit must be an ally of the POV player. </summary>
    Allied,
    /// <summary> The unit must be an enemy of the POV player. </summary>
    Enemy,
    /// <summary> Owned by the player themselves. </summary>
    Self
}