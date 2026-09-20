using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// Defines the win, loss, or neutral state of a player or scenario.
/// </summary>
public enum WinLossState : UInt32
{
    /// <summary>The game state is neutral or ongoing.</summary>
    None = 0,
    /// <summary>The player has won.</summary>
    Win = 1,
    /// <summary>The player has lost.</summary>
    Loss = 2
}