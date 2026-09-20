using R3;
using SHCDESE.Logging;
using System;
using System.Collections.Generic;

namespace SHCDESE.API.Components.Timer;

/// <summary>
/// A custom R3 FrameProvider that serves as the central "heartbeat" for all reactive operations in the script extender.
/// It is ticked by the game's main simulation loop and provides access to the current <see cref="FrameState"/>.
/// </summary>
public sealed class StrongholdFrameProvider : FrameProvider
{
    private long _gameTick;
    private long _totalGameTimeUnits;
    private readonly List<IFrameRunnerWorkItem> _active = new();
    private readonly List<IFrameRunnerWorkItem> _newItems = new();

    /// <summary>
    /// Gets a snapshot of the complete engine state for the current frame.
    /// </summary>
    public FrameState CurrentFrameState { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the game is currently paused. This is a convenience property from <see cref="CurrentFrameState"/>.
    /// </summary>
    public bool IsPaused => CurrentFrameState.IsPaused;

    /// <summary>
    /// Gets the current game tick.
    /// </summary>
    public int CurrentGameTick => (int)_gameTick;

    /// <summary>
    /// Direct, low-overhead event that fires on every simulated game tick.
    /// Passes the current GameTick as the argument.
    /// Only fires if the game is NOT paused.
    /// </summary>
    public event Action<int>? OnGameTick;

    /// <summary>
    /// Gets the total number of frames that have been processed.
    /// </summary>
    /// <remarks>
    /// For compatibility with R3, this returns the game's deterministic tick count (simTickCount), not a render frame count.
    /// </remarks>
    public override long GetFrameCount() => _gameTick;
    public long GetCurrentGameTime() => _totalGameTimeUnits;

    /// <summary>
    /// Registers a new work item (e.g., an Observable subscription) to be processed on each frame tick.
    /// This method is called automatically by R3 when a new frame-based subscription is created.
    /// </summary>
    /// <param name="item">The work item to register.</param>
    public override void Register(IFrameRunnerWorkItem item)
    {
        lock (_newItems)
        {
            _newItems.Add(item);
        }
    }

    /// <summary>
    /// Advances the frame scheduler by one step, updating the internal game tick count.
    /// This method should be called exactly once per simulation tick from the main game loop hook.
    /// </summary>
    /// <param name="state">The complete state of the engine for the current tick, passed by `in` reference to avoid copying.</param>
    public void Tick(in FrameState state)
    {
        // Update the current state and increment the frame count.
        this.CurrentFrameState = state;
        _gameTick = state.GameTick;
        _totalGameTimeUnits = state.TotalGameTimeUnits;

        // Safely move newly registered items from the waiting list to the active list.
        lock (_newItems)
        {
            if (_newItems.Count > 0)
            {
                _active.AddRange(_newItems);
                _newItems.Clear();
            }
        }

        // Iterate through all active work items and advance their state.
        for (int i = 0; i < _active.Count;)
        {
            IFrameRunnerWorkItem item = _active[i];

            // MoveNext returns false when the operation (e.g., timer, sequence) is complete.
            if (!item.MoveNext(_gameTick))
            {
                // If complete, remove the item from the active list to stop processing it.
                _active.RemoveAt(i);
                continue; // Do not increment 'i' as the list has shifted.
            }

            i++;
        }

        if (!state.IsPaused)
        {
            try
            {
                OnGameTick?.Invoke((int)_gameTick);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "Error executing OnGameTick subscriber.");
            }
        }
    }
}
