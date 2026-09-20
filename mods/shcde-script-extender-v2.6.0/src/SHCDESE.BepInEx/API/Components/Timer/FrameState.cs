using System;

namespace SHCDESE.API.Components.Timer;

/// <summary>
/// Represents a comprehensive, allocation-free snapshot of the game engine's state for a single frame.
/// </summary>
/// <remarks>
/// This is a <c>readonly struct</c> to ensure it is passed by value (or by `in` reference) on the stack,
/// avoiding heap allocations and garbage collection pressure within the high-frequency game loop.
/// </remarks>
public readonly struct FrameState
{
    /// <summary>
    /// The currently being processed game tick.
    /// </summary>
    public int GameTick { get; }
    public long TotalGameTimeUnits { get; } // NEW

    /// <summary>
    /// Gets a value indicating whether the landscape is currently flattened.
    /// </summary>
    public bool FlattenedLandscape { get; }

    /// <summary>
    /// Gets a value indicating whether the game is currently paused.
    /// </summary>
    public bool IsPaused { get; }

    /// <summary>
    /// Gets the X coordinate of the tile the mouse is currently hovering over.
    /// </summary>
    public int MouseOverX { get; }

    /// <summary>
    /// Gets the Y coordinate of the tile the mouse is currently hovering over.
    /// </summary>
    public int MouseOverY { get; }

    /// <summary>
    /// Gets a value indicating whether the Shift key is currently pressed.
    /// </summary>
    public bool IsShiftPressed { get; }

    /// <summary>
    /// Gets a value indicating whether the Control (Ctrl) key is currently pressed.
    /// </summary>
    public bool IsCtrlPressed { get; }

    /// <summary>
    /// Gets a value indicating whether the Alt key is currently pressed.
    /// </summary>
    public bool IsAltPressed { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameState"/> struct with a snapshot of the engine's current state.
    /// </summary>
    public FrameState(int gameTick, long totalGameTimeUnits, bool isPaused)
    {
        GameTick = gameTick;
        TotalGameTimeUnits = totalGameTimeUnits; // NEW
        IsPaused = isPaused;
    }
}