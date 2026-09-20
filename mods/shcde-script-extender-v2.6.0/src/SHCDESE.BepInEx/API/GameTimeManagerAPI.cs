using R3;
using RedBird.X64.Memory;
using SHCDESE.API.Components.Timer;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.GameGlobals;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Threading;
namespace SHCDESE.API;

/// <summary>
/// Provides a high-level API for interacting with the game's internal date and time system.
/// </summary>
/// <remarks>
/// This class is a singleton that allows for reading and modifying the in-game calendar (day, month, year).
/// It also serves as the host for the core <see cref="TimerEngine"/> and <see cref="StrongholdFrameProvider"/>,
/// which are fundamental for all time-based operations and events in the modding framework.
/// </remarks>
[LuaApiNamespace("Time")]
public unsafe sealed class GameTimeManagerAPI
{
#pragma warning disable 0618
    private static readonly Lazy<GameTimeManagerAPI> _lazy = new(() => new GameTimeManagerAPI());
    public static GameTimeManagerAPI Instance => _lazy.Value;

    /// <summary>Gets a memory wrapper for the current in-game year.</summary>
    internal readonly RVAMemoryValue<UInt32> _currentYear;

    /// <summary>Gets a memory wrapper for the current in-game month.</summary>
    internal readonly RVAMemoryValue<UInt32> _currentMonth;

    /// <summary>Gets a memory wrapper for the current in-game day.</summary>
    internal readonly RVAMemoryValue<UInt32> _currentDay;

    /// <summary>Gets a memory wrapper for the configured number of days in a month.</summary>
    internal readonly RVAMemoryValue<Int16> _daysInMonth;

    /// <summary>Gets a memory wrapper for the configured number of months in a year.</summary>
    internal readonly RVAMemoryValue<Int16> _monthsInYear;

    /// <summary>
    /// A reactive property that updates with the current in-game day.
    /// </summary>
    [LuaApiExport("OnDayChanged")]
    public static ReactiveProperty<UInt32> CurrentDayReactive;

    /// <summary>
    /// A reactive property that updates with the current in-game month.
    /// </summary>
    [LuaApiExport("OnMonthChanged")]
    public static ReactiveProperty<UInt32> CurrentMonthReactive;

    /// <summary>
    /// A reactive property that updates with the current in-game year.
    /// </summary>
    [LuaApiExport("OnYearChanged")]
    public static ReactiveProperty<UInt32> CurrentYearReactive;

    internal readonly StrongholdFrameProvider _frameProvider;
    internal readonly TimerEngine _timerEngine;

    private UInt64* _elapsedMapTicks = null;

    private int _initialized = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameTileManagerAPI"/> class.
    /// This constructor is private to enforce the singleton pattern.
    /// </summary>
    private GameTimeManagerAPI()
    {
        _frameProvider = new StrongholdFrameProvider();
        _timerEngine = new TimerEngine(_frameProvider);

        _currentYear = RVAMemoryValue<UInt32>.From(GameGlobalsManager.Instance.DateTimeCurrentYearRVA!, baseAddress: GameGlobalsManager.Instance.GamePlayerManagerVA);
        _currentMonth = RVAMemoryValue<UInt32>.From(GameGlobalsManager.Instance.DateTimeCurrentMonthRVA!, baseAddress: GameGlobalsManager.Instance.GamePlayerManagerVA);
        _currentDay = RVAMemoryValue<UInt32>.From(GameGlobalsManager.Instance.DateTimeCurrentDayRVA!, baseAddress: GameGlobalsManager.Instance.GamePlayerManagerVA);
        _daysInMonth = RVAMemoryValue<Int16>.From(GameGlobalsManager.Instance.DateTimeDaysInMonthRVA!, baseAddress: GameGlobalsManager.Instance.GamePlayerManagerVA);
        _monthsInYear = RVAMemoryValue<Int16>.From(GameGlobalsManager.Instance.DateTimeMonthsInYearRVA!, baseAddress: GameGlobalsManager.Instance.GamePlayerManagerVA);

        CurrentDayReactive = new ReactiveProperty<UInt32>(0);
        CurrentMonthReactive = new ReactiveProperty<UInt32>(0);
        CurrentYearReactive = new ReactiveProperty<UInt32>(0);

        _elapsedMapTicks = (UInt64*)(GameGlobalsManager.Instance.ElapsedMapTicksVA);
    }

    internal static void InitializeSubscribers()
    {
        if (Interlocked.Exchange(ref Instance._initialized, 1) != 0)
            return;

        LogHelper.Information($"Setting up subscribers");

        MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnUnloadMap);
    }

    /// <summary>
    /// Event handler called when a map is unloaded.
    /// </summary>
    private static void OnUnloadMap(MapUnloadEventArgs args)
    {
        LogHelper.Information($"Unloading");
        Instance._timerEngine.RemoveAllTimers();
    }

    /// <summary>
    /// Gets the core frame provider that supplies the deterministic game tick and pause state information.
    /// </summary>
    /// <returns>The singleton instance of the <see cref="StrongholdFrameProvider"/>.</returns>
    public StrongholdFrameProvider GetFrameProvider() => _frameProvider;

    /// <summary>
    /// Gets the core timer engine for scheduling deterministic delayed and repeated actions.
    /// </summary>
    /// <returns>The singleton instance of the <see cref="TimerEngine"/>.</returns>
    public TimerEngine GetTimerEngine() => _timerEngine;

    /// <summary>
    /// This internal method is called by a detour on the game's native date update function.
    /// It pushes the latest date values into reactive properties for other systems to observe.
    /// </summary>
    internal void UpdateDateTime()
    {
        CurrentDayReactive.Value = _currentDay.Value;
        CurrentMonthReactive.Value = _currentMonth.Value;
        CurrentYearReactive.Value = _currentYear.Value;
    }

    /// <summary>
    /// Use this for logic that must happen every tick.
    /// <para>This event does NOT fire when the game is paused.</para>
    /// </summary>
    public event Action<int> OnTick
    {
        add => _frameProvider.OnGameTick += value;
        remove => _frameProvider.OnGameTick -= value;
    }

    //
    // In-game time related functions
    //

    /// <summary>Gets the current in-game year.</summary>
    [LuaApiExport("GetCurrentYear")]
    public UInt32 GetCurrentYear() => _currentYear.Value;

    /// <summary>Sets the current in-game year.</summary>
    [LuaApiExport("SetCurrentYear")]
    public void SetCurrentYear(UInt32 year) => _currentYear.Value = year;

    /// <summary>Gets the current in-game month.</summary>
    [LuaApiExport("GetCurrentMonth")]
    public UInt32 GetCurrentMonth() => _currentMonth.Value;

    /// <summary>Sets the current in-game month.</summary>
    [LuaApiExport("SetCurrentMonth")]
    public void SetCurrentMonth(UInt32 month) => _currentMonth.Value = month;

    /// <summary>Gets the current in-game day.</summary>
    [LuaApiExport("GetCurrentDay")]
    public UInt32 GetCurrentDay() => _currentDay.Value;

    /// <summary>Sets the current in-game day.</summary>
    [LuaApiExport("SetCurrentDay")]
    public void SetCurrentDay(UInt32 day) => _currentDay.Value = day;

    /// <summary>Gets the configured number of days in a month.</summary>
    [LuaApiExport("GetDaysInMonth")]
    public int GetDaysInMonth() => _daysInMonth.Value;

    /// <summary>Sets the number of days in a month.</summary>
    [LuaApiExport("SetDaysInMonth")]
    public void SetDaysInMonth(Int16 days) => _daysInMonth.Value = days;

    /// <summary>Gets the configured number of months in a year.</summary>
    [LuaApiExport("GetMonthsInYear")]
    public int GetMonthsInYear() => _monthsInYear.Value;

    /// <summary>Sets the number of months in a year.</summary>
    [LuaApiExport("SetMonthsInYear")]
    public void SetMonthsInYear(Int16 months) => _monthsInYear.Value = months;

    /// <summary>
    /// Captures the current deterministic game time as a lightweight stamp.
    /// Store the returned value and later pass it to the Has...Elapsed family of methods.
    /// </summary>
    /// <returns>A <see cref="GameTimeStamp"/> representing "now".</returns>
    /// <example>
    /// C#:
    /// <code>
    /// var stamp = GameTimeManagerAPI.Instance.CaptureTimeStamp();
    /// // ... later ...
    /// if (GameTimeManagerAPI.Instance.HasMillisecondsElapsed(stamp, 2000))
    ///     DoSomething();
    /// </code>
    /// </example>
    [LuaApiExport("CaptureTimeStamp")]
    public GameTimeStamp CaptureTimeStamp() => new GameTimeStamp(_frameProvider.GetCurrentGameTime(), _frameProvider.CurrentGameTick);

    /// <summary>
    /// Returns the raw number of high-precision game time units that have elapsed
    /// since <paramref name="stamp"/> was captured.
    /// 1 second = 1,000,000 units.
    /// </summary>
    [LuaApiExport("GetElapsedGameTimeUnits")]
    public long GetElapsedGameTimeUnits(GameTimeStamp stamp) => stamp.GetElapsedGameTimeUnits(_frameProvider.GetCurrentGameTime());

    /// <summary>
    /// Returns the number of whole milliseconds that have elapsed since <paramref name="stamp"/> was captured.
    /// </summary>
    [LuaApiExport("GetElapsedMilliseconds")]
    public long GetElapsedMilliseconds(GameTimeStamp stamp) => stamp.GetElapsedMilliseconds(_frameProvider.GetCurrentGameTime());

    /// <summary>
    /// Returns <c>true</c> if at least <paramref name="milliseconds"/> of in-game time have
    /// elapsed since <paramref name="stamp"/> was captured.
    /// </summary>
    /// <param name="stamp">The reference point in time.</param>
    /// <param name="milliseconds">The threshold in milliseconds to test against.</param>
    [LuaApiExport("HasMillisecondsElapsed")]
    public bool HasMillisecondsElapsed(GameTimeStamp stamp, int milliseconds) => GetElapsedMilliseconds(stamp) >= milliseconds;

    /// <summary>
    /// Returns <c>true</c> if at least <paramref name="ticks"/> game ticks have elapsed
    /// since <paramref name="stamp"/> was captured.
    /// </summary>
    /// <remarks>
    /// This uses the raw game-tick counter, which increments once per simulation step
    /// regardless of frame rate. It is the coarser (but simplest) way to measure time.
    /// For precise sub-tick accuracy, prefer <see cref="HasMillisecondsElapsed"/>.
    /// </remarks>
    /// <param name="stamp">The reference point in time.</param>
    /// <param name="ticks">The number of game ticks to test against (e.g. 2000).</param>
    [LuaApiExport("HasTicksElapsed")]
    public bool HasTicksElapsed(GameTimeStamp stamp, int ticks)
    {
        if (stamp.IsEmpty) return false;
        int elapsed = _frameProvider.CurrentGameTick - stamp.CapturedGameTick;
        return elapsed >= ticks;
    }

    /// <summary>
    /// Returns how many game ticks have elapsed since <paramref name="stamp"/> was captured.
    /// </summary>
    [LuaApiExport("GetElapsedTicks")]
    public int GetElapsedTicks(GameTimeStamp stamp)
    {
        if (stamp.IsEmpty) return 0;
        int elapsed = _frameProvider.CurrentGameTick - stamp.CapturedGameTick;
        return elapsed > 0 ? elapsed : 0;
    }

    /// <summary>
    /// Returns how many game ticks have elapsed since start of map.
    /// Preserved across save-files.
    /// </summary>
    /// <returns>The elapsed ticks since start of map; Otherwise 0.</returns>
    [LuaApiExport("GetElapsedMapTicks")]
    public int GetElapsedMapTicks()
    {
        if (_elapsedMapTicks == null)
        {
            LogHelper.Error($"_elapsedMapTicks is null");
            return 0;
        }
        return (int)*_elapsedMapTicks;
    }

#pragma warning restore 0618
}
