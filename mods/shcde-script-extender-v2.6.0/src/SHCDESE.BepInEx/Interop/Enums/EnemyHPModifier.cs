using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// Enemy Health modifier. Directly associated with the Advanced Option.
/// </summary>
public enum EnemyHPModifier
{
    Weak = 0,   // 66%
    Normal = 1, // 100%
    Strong = 2, // 125%
    VeryStrong = 3 // 150%
}
