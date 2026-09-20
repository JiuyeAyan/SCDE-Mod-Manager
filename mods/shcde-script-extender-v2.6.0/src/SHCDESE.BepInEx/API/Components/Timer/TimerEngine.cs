using MessagePack;
using NLua;
using R3;
using SHCDESE.Logging;
using SHCDESE.Lua;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace SHCDESE.API.Components.Timer;

/// <summary>
/// A deterministic, high-precision timer engine for Stronghold Crusader.
/// </summary>
/// <remarks>
/// This engine is synchronized with the game's core simulation loop via the <see cref="DeterministicClock"/>.
/// This approach ensures all timed events are deterministic and desync-free (At least I think so) in multiplayer.
/// It also handles serialization, allowing timers to be saved and loaded with the game state.
/// </remarks>
public sealed class TimerEngine
{
    private readonly StrongholdFrameProvider _frameProvider;
    private readonly ConcurrentDictionary<string, TrackedTimer> _activeTimers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TimerEngine"/> class.
    /// </summary>
    /// <param name="frameProvider">The game's custom R3 FrameProvider, which dictates the engine's tick rate and provides the current game tick.</param>
    public TimerEngine(StrongholdFrameProvider frameProvider)
    {
        _frameProvider = frameProvider;
    }

    /// <summary>
    /// The filename used when storing serialized timer data within a map archive.
    /// </summary>
    internal const string MAP_ARCHIVE_IDENTIFIER_FILENAME = "_SE_Timers.msgpack";

    /// <summary>
    /// Internal class to hold the complete state of a running timer, including its subscription and progress.
    /// </summary>
    private class TrackedTimer : IDisposable
    {
        public string HandleId { get; init; }
        public string CallbackName { get; init; }
        public TimerType Type { get; init; }
        public IDisposable Subscription { get; set; }
        public long TargetGameTime { get; set; }
        public long IntervalGameTime { get; init; }

        public void Dispose() => Subscription?.Dispose();
    }

    /// <summary>
    /// Schedules a single action to be executed once after a specified duration.
    /// To make a timer savable, a unique <paramref name="callbackName"/> must be provided.
    /// </summary>
    /// <param name="milliseconds">The delay in milliseconds, which will be converted into deterministic game ticks.</param>
    /// <param name="action">The C# Action to execute.</param>
    /// <param name="callbackName">A unique string name for the callback. Timers with a null or empty name will not be saved.</param>
    /// <returns>A unique string handle for the timer.</returns>
    public string AddDelayedAction(int milliseconds, Action action, string callbackName)
    {
        string handleId = Guid.NewGuid().ToString();

        long intervalGameTime = (long)milliseconds * 1000L;
        long currentGameTime = _frameProvider.GetCurrentGameTime();

        TrackedTimer timer = new TrackedTimer
        {
            HandleId = handleId,
            CallbackName = callbackName,
            Type = TimerType.Delayed,
            IntervalGameTime = intervalGameTime,
            TargetGameTime = currentGameTime + intervalGameTime,
        };

        timer.Subscription = CreateSubscription(timer, action);
        _activeTimers[handleId] = timer;
        return handleId;
    }

    /// <summary>
    /// Schedules a recurring action. To make it savable, a unique <paramref name="callbackName"/> must be provided.
    /// </summary>
    /// <param name="milliseconds">The interval in milliseconds, which will be converted into deterministic game ticks.</param>
    /// <param name="action">The C# Action to execute.</param>
    /// <param name="callbackName">A unique string name for the callback. Timers with a null or empty name will not be saved.</param>
    /// <returns>A unique string handle for the timer.</returns>
    public string AddRepeatedAction(int milliseconds, Action action, string callbackName)
    {
        string handleId = Guid.NewGuid().ToString();

        long intervalGameTime = (long)milliseconds * 1000L;
        long currentGameTime = _frameProvider.GetCurrentGameTime();

        TrackedTimer timer = new TrackedTimer
        {
            HandleId = handleId,
            CallbackName = callbackName,
            Type = TimerType.Repeated,
            IntervalGameTime = intervalGameTime,
            TargetGameTime = currentGameTime + intervalGameTime,
        };

        timer.Subscription = CreateSubscription(timer, action);
        _activeTimers[handleId] = timer;
        return handleId;
    }


    /// <summary>
    /// Creates the underlying R3 subscription for a given timer.
    /// </summary>
    private IDisposable CreateSubscription(TrackedTimer timer, Action callback)
    {
        Observable<Unit> observable = Observable.EveryUpdate(_frameProvider);

        observable = observable.Where(_ => !_frameProvider.IsPaused);
        return observable.Subscribe(_ =>
        {
            // We now check against our high-precision clock.
            long currentGameTime = _frameProvider.GetCurrentGameTime();

            if (currentGameTime >= timer.TargetGameTime)
            {
                HandleAction(callback, timer.HandleId, timer.Type == TimerType.Delayed);

                if (timer.Type == TimerType.Repeated)
                {
                    // Set the next target time based on the previous target to prevent drift.
                    timer.TargetGameTime += timer.IntervalGameTime;
                }
            }
        });
    }

    /// <summary>
    /// Serializes the state of all savable timers to a MessagePack byte array in a deterministic format.
    /// </summary>
    public byte[] Serialize()
    {
        long currentGameTime = _frameProvider.GetCurrentGameTime();
        return MessagePackSerializer.Serialize(new TimerSaveDataHolder
        {
            TimerSaveData = _activeTimers.Values
                .Where(t => !string.IsNullOrEmpty(t.CallbackName))
                .Select(t => new TimerSaveData
                {
                    HandleId = t.HandleId,
                    CallbackName = t.CallbackName,
                    Type = t.Type,
                    OriginalIntervalInGameTime = t.IntervalGameTime,
                    GameTimeRemainingOrElapsed = t.Type == TimerType.Delayed
                        ? t.TargetGameTime - currentGameTime
                        : currentGameTime - (t.TargetGameTime - t.IntervalGameTime),
                }).ToList()
        });
    }

    /// <summary>
    /// Clears all current timers and restores a new set from a MessagePack byte array.
    /// </summary>
    public void LoadFromMessagePack(byte[] bytes)
    {
        foreach (TrackedTimer timer in _activeTimers.Values) { timer.Dispose(); }
        _activeTimers.Clear();

        if (bytes == null || bytes.Length == 0) return;

        TimerSaveDataHolder timerSaveDataHolder = MessagePackSerializer.Deserialize<TimerSaveDataHolder>(bytes);
        if (timerSaveDataHolder?.TimerSaveData == null) return;

        long currentGameTime = _frameProvider.GetCurrentGameTime();

        foreach (TimerSaveData data in timerSaveDataHolder.TimerSaveData)
        {
            Action callback = CreateLuaCallback(data.CallbackName);
            if (callback == null) { continue; }

            long targetGameTime = (data.Type == TimerType.Delayed)
                ? currentGameTime + data.GameTimeRemainingOrElapsed
                : (currentGameTime - data.GameTimeRemainingOrElapsed) + data.OriginalIntervalInGameTime;

            TrackedTimer timer = new TrackedTimer
            {
                HandleId = data.HandleId,
                CallbackName = data.CallbackName,
                Type = data.Type,
                IntervalGameTime = data.OriginalIntervalInGameTime,
                TargetGameTime = targetGameTime
            };

            timer.Subscription = CreateSubscription(timer, callback);
            _activeTimers[data.HandleId] = timer;
        }
    }

    /// <summary>
    /// The internal "Callback Provider". Takes a function name and returns
    /// a C# Action that will execute that function in the current Lua state.
    /// </summary>
    private Action CreateLuaCallback(string callbackName)
    {
        return () =>
        {
            try
            {
                // Directly use the singleton to access the Lua state.
                LuaFunction? func = LuaManager.Instance.Lua?[callbackName] as LuaFunction;
                if (func != null)
                {
                    func.Call();
                    func.Dispose();
                }
                else
                {
                    LogHelper.Error($"Timer callback error: Could not find global Lua function '{callbackName}'.");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "An exception occurred executing Lua timer callback for '{FunctionName}'.", callbackName);
            }
        };
    }

    /// <summary>
    /// Removes all active timers
    /// </summary>
    public void RemoveAllTimers()
    {
        foreach (TrackedTimer timer in _activeTimers.Values)
        {
            timer.Dispose();
        }

        _activeTimers.Clear(); 
    }

    /// <summary>
    /// Returns active timers amount
    /// </summary>
    /// <returns>The amount of active timers.</returns>
    public int GetTimersCount()
    {
        return _activeTimers.Count;
    }

    /// <summary>
    /// Removes and cancels any timer using its string handle.
    /// </summary>
    /// <param name="handleId">The string handle of the timer to remove.</param>
    public void RemoveAction(string handleId)
    {
        try
        {
            if (!string.IsNullOrEmpty(handleId) && _activeTimers.TryRemove(handleId, out TrackedTimer? timer))
            {
                timer.Dispose();
            }
        }  
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error during removal of action");
        }
    }

    /// <summary>
    /// Checks if a timer with the specified handle ID is currently active.
    /// </summary>
    /// <param name="handleId">The string handle of the timer to check.</param>
    /// <returns><c>true</c> if the timer exists and is active; otherwise, <c>false</c>.</returns>
    public bool IsHandleValid(string handleId)
    {
        if (!string.IsNullOrEmpty(handleId) && _activeTimers.ContainsKey(handleId))
            return true;

        return false;
    }

    private void HandleAction(Action action, string handleId, bool isDelayed)
    {
        try
        {
            action.Invoke();
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"An exception occurred within a timer action for handle {handleId}");
        }
        finally
        {
            if (isDelayed)
            {
                RemoveAction(handleId);
            }
        }
    }
}