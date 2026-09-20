using R3;
using SHCDESE.API.Components.Trigger;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Trigger;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SHCDESE.API;

/// <summary>
/// A high-performance system for detecting when game entities enter or exit defined areas (triggers).
/// </summary>
/// <remarks>
/// This manager uses a spatial partitioning grid to efficiently check entity positions against trigger zones.
/// Instead of a brute-force check of every entity against every trigger each frame, it drastically reduces
/// the number of checks by only considering triggers in the same grid cell as the entity.
/// This class should be updated once per game frame via the <see cref="Update"/> method.
/// </remarks>
[LuaApiNamespace("Trigger")]
public unsafe sealed class GameTriggerManager
{
    private static readonly Lazy<GameTriggerManager> _lazy = new(() => new GameTriggerManager());
    public static GameTriggerManager Instance => _lazy.Value;

    // --- Grid Configuration ---

    /// <summary>
    /// The total size of the map in local tile units (the coarse-grained grid).
    /// </summary>

    /// <summary>The total size of the map in local tile units (the coarse-grained grid).</summary>
    private const int MAP_SIZE_LOCAL = 800;
    /// <summary>The size of a single spatial grid cell in local tile units. Changing this affects performance, beware.</summary>
    private const int GRID_CELL_SIZE = 20;
    /// <summary>The width of the spatial grid in cells (Map Size / Cell Size).</summary>
    private const int GRID_WIDTH = MAP_SIZE_LOCAL / GRID_CELL_SIZE;
    /// <summary>The height of the spatial grid in cells (Map Size / Cell Size).</summary>
    private const int GRID_HEIGHT = MAP_SIZE_LOCAL / GRID_CELL_SIZE;

    // --- State ---

    /// <summary>The spatial grid. Each cell holds a list of triggers that overlap it.</summary>
    private readonly List<TriggerZone>[,] _grid = new List<TriggerZone>[GRID_WIDTH, GRID_HEIGHT];

    /// <summary>A central dictionary containing all registered triggers, keyed by their unique ID.</summary>
    private readonly Dictionary<int, TriggerZone> _allTriggers = new Dictionary<int, TriggerZone>();

    /// <summary>A simple counter to generate unique, sequential integer handles.</summary>
    private int _nextHandle = 1;

    private int _initialized = 0;

    /// <summary>
    /// Retrieves and increments the next available handle value.
    /// </summary>
    /// <returns>The next integer handle value before it is incremented.</returns>
    private int GetNextHandle()
    {
        return _nextHandle++;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameTriggerManager"/> class.
    /// This constructor is private to enforce the singleton pattern.
    /// </summary>
    private GameTriggerManager()
    {
        // Initialize the grid cells with empty lists to prevent null reference exceptions.
        for (int x = 0; x < GRID_WIDTH; x++)
            for (int y = 0; y < GRID_HEIGHT; y++)
                _grid[x, y] = new List<TriggerZone>();
    }

    internal static void InitializeSubscribers()
    {
        if (Interlocked.Exchange(ref Instance._initialized, 1) != 0)
            return;

        LogHelper.Information($"Setting up subscribers");

        MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnUnloadMap);
    }

    internal void Unload()
    {
        Instance.RemoveAllTriggers();
    }

    /// <summary>
    /// Event handler called when a map is unloaded.
    /// </summary>
    private static void OnUnloadMap(MapUnloadEventArgs args)
    {
        LogHelper.Information($"Unloading");
        Instance.Unload();
    }

    /// <summary>
    /// Get all currently active trigger ids
    /// </summary>
    /// <returns>Trigger Ids</returns>
    [LuaApiExport("GetAll")]
    public int[] GetAllTriggers()
    {
        return [.. _allTriggers.Keys];
    }

    /// <summary>
    /// Removes all currently active triggers
    /// </summary>
    [LuaApiExport("RemoveAll")]
    public void RemoveAllTriggers()
    {
        foreach (int triggerId in GetAllTriggers())
        {
            if (!RemoveTrigger(triggerId))
            {
                LogHelper.Warning($"Failed to remove trigger id {triggerId}");
            }
        }
    }

    /// <summary>
    /// Creates and adds a new rectangular trigger zone to the system.
    /// </summary>
    /// <param name="x">The top-left X-coordinate of the rectangle (in local tile units).</param>
    /// <param name="y">The top-left Y-coordinate of the rectangle (in local tile units).</param>
    /// <param name="width">The width of the rectangle (in local tile units).</param>
    /// <param name="height">The height of the rectangle (in local tile units).</param>
    /// <returns><c>Trigger Handle</c> if the trigger was added; otherwise, <c>0</c>.</returns>
    [LuaApiExport("CreateRectZone")]
    public int AddRectTrigger(int x, int y, int width, int height)
    {
        int handle = GetNextHandle();
        if (!AddTrigger(new RectTriggerZone(handle, x, y, width, height)))
        {
            return 0;
        }
        return handle;
    }

    /// <summary>
    /// Creates and adds a new circular trigger zone to the system.
    /// </summary>
    /// <param name="centerX">The center X-coordinate of the circle (in local tile units).</param>
    /// <param name="centerY">The center Y-coordinate of the circle (in local tile units).</param>
    /// <param name="radius">The radius of the circle (in local tile units).</param>
    /// <returns><c>Trigger Handle</c> if the trigger was added; otherwise, <c>0</c>.</returns>
    [LuaApiExport("CreateCircleZone")]
    public int AddCircleTrigger(int centerX, int centerY, int radius)
    {
        int handle = GetNextHandle();
        if (!AddTrigger(new CircleTriggerZone(handle, centerX, centerY, radius)))
        {
            return 0;
        }
        return handle;
    }

    /// <summary>
    /// Adds a pre-constructed trigger zone to the system.
    /// </summary>
    /// <param name="zone">The trigger zone object to add.</param>
    /// <returns><c>true</c> if the trigger was added; otherwise, <c>false</c>.</returns>
    public bool AddTrigger(TriggerZone zone)
    {
        if (zone == null)
        {
            LogHelper.Error("Attempted to add a null trigger zone");
            return false;
        }

        if (_allTriggers.ContainsKey(zone.Handle))
        {
            LogHelper.Error($"A trigger zone with handle [{zone.Handle}] already exists. Ignoring");
            return false;
        }

        _allTriggers[zone.Handle] = zone;

        // Determine which grid cells this zone overlaps with and add it to them.
        if (zone is RectTriggerZone rectZone)
        {
            // Convert the zone's world coordinates into grid cell indices.
            // Clamp the values to be within the grid's bounds.
            int startX = Math.Max(0, rectZone.X / GRID_CELL_SIZE);
            int startY = Math.Max(0, rectZone.Y / GRID_CELL_SIZE);
            int endX = Math.Min(GRID_WIDTH - 1, (rectZone.X + rectZone.Width - 1) / GRID_CELL_SIZE);
            int endY = Math.Min(GRID_HEIGHT - 1, (rectZone.Y + rectZone.Height - 1) / GRID_CELL_SIZE);

            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    _grid[x, y].Add(zone);
                }
            }
        }
        else if (zone is CircleTriggerZone circleZone)
        {
            // Define a bounding box around the circle to find all potentially overlapping cells.
            int startX = Math.Max(0, (circleZone.CenterX - circleZone.Radius) / GRID_CELL_SIZE);
            int startY = Math.Max(0, (circleZone.CenterY - circleZone.Radius) / GRID_CELL_SIZE);
            int endX = Math.Min(GRID_WIDTH - 1, (circleZone.CenterX + circleZone.Radius) / GRID_CELL_SIZE);
            int endY = Math.Min(GRID_HEIGHT - 1, (circleZone.CenterY + circleZone.Radius) / GRID_CELL_SIZE);

            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    // For circles, this check is an optimization. A more precise check would be to see
                    // if the circle intersects the cell's rectangle. However, simply adding it to all
                    // cells in the bounding box is sufficient and much simpler to implement.
                    _grid[x, y].Add(zone);
                }
            }
        }
        else
        {
            LogHelper.Warning($"This function does not support zone type {zone.GetType().Name}");
        }
        return true;
    }

    /// <summary>
    /// Removes a trigger zone from the system using its unique ID.
    /// </summary>
    /// <param name="triggerHandle">The unique handle of the trigger zone to remove.</param>
    /// <returns><c>true</c> if the trigger was found and removed; otherwise, <c>false</c>.</returns>
    [LuaApiExport("RemoveZone")]
    public bool RemoveTrigger(int triggerHandle)
    {
        if (triggerHandle <= 0 || !_allTriggers.TryGetValue(triggerHandle, out TriggerZone zoneToRemove))
        {
            LogHelper.Error("Attempted to remove a null or ID-less trigger zone");
            return false; // Trigger not found.
        }

        // Remove the trigger from the central dictionary.
        _allTriggers.Remove(triggerHandle);

        // Now, remove the trigger from all grid cells it was a part of.
        if (zoneToRemove is RectTriggerZone rectZone)
        {
            // We must calculate the same grid cell bounds as when we added it.
            int startX = Math.Max(0, rectZone.X / GRID_CELL_SIZE);
            int startY = Math.Max(0, rectZone.Y / GRID_CELL_SIZE);
            int endX = Math.Min(GRID_WIDTH - 1, (rectZone.X + rectZone.Width - 1) / GRID_CELL_SIZE);
            int endY = Math.Min(GRID_HEIGHT - 1, (rectZone.Y + rectZone.Height - 1) / GRID_CELL_SIZE);

            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    // This is a safe removal from the list in each grid cell.
                    _grid[x, y].Remove(zoneToRemove);
                }
            }
        }
        else if (zoneToRemove is CircleTriggerZone circleZone)
        {
            // We must calculate the exact same bounding box as when we added it.
            int startX = Math.Max(0, (circleZone.CenterX - circleZone.Radius) / GRID_CELL_SIZE);
            int startY = Math.Max(0, (circleZone.CenterY - circleZone.Radius) / GRID_CELL_SIZE);
            int endX = Math.Min(GRID_WIDTH - 1, (circleZone.CenterX + circleZone.Radius) / GRID_CELL_SIZE);
            int endY = Math.Min(GRID_HEIGHT - 1, (circleZone.CenterY + circleZone.Radius) / GRID_CELL_SIZE);

            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    // Safely remove the zone from the list in each grid cell it might have been in.
                    _grid[x, y].Remove(zoneToRemove);
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Executes one update tick of the trigger system. This should be called once per game frame.
    /// </summary>
    /// <remarks>
    /// This method performs three main tasks:
    /// <para>Check Phase: Iterates through all active game units, determines their grid cell, and checks them against the small list of potential triggers in that cell. It identifies all "OnEnter" events during this phase.</para>
    /// <para>State Update: It builds a complete set of all entities that are inside any trigger during the current frame.</para>
    /// <para>Exit Detection: It compares the current frames state with each triggers previous frames state to efficiently find all "OnExit" events.</para>
    /// </remarks>
    public void Update()
    {
        // A temporary set to track all entities that are inside any trigger this frame.
        // Using pointers (UInt64) as the key is extremely fast as it avoids string comparisons or complex hashing.
        HashSet<UInt64> allEntitiesInZonesNow = new HashSet<UInt64>();

        // --- PHASE 1: CHECK FOR ENTERS ---
        // Iterate through the entire units array directly via Span.
        // This avoids creating any intermediate collections or dictionaries.
        Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
        for (int i = 0; i < units.Length; i++)
        {
            // Get a reference to the unit to avoid struct copies.
            ref readonly GameUnit unit = ref units[i];

            // Ignore dead or inactive units immediately.
            if (unit.r_AliveState != AliveState.IsAlive) 
                continue;

            // Get the units position and its memory address, which we use as a unique ID.
            UnmanagedVector2<UInt16> position = new UnmanagedVector2<UInt16>(unit.r_CurrentTilePositionX, unit.r_CurrentTilePositionY);
            UInt64 entityPtr = (UInt64)Unsafe.AsPointer(ref Unsafe.AsRef(in unit));

            // Convert the unit local tile position into a grid cell coordinate.
            int cellX = position.X / GRID_CELL_SIZE;
            int cellY = position.Y / GRID_CELL_SIZE;

            // Bounds check to ensure the unit is on the grid.
            if (cellX < 0 || cellX >= GRID_WIDTH || cellY < 0 || cellY >= GRID_HEIGHT) 
                continue;

            // This is the core optimization: retrieve the short list of potential triggers
            // that overlap this specific grid cell, instead of checking all triggers on the map.
            List<TriggerZone> potentialTriggers = _grid[cellX, cellY];

            foreach (TriggerZone trigger in potentialTriggers)
            {
                // Perform the final check to see if the position is inside the triggers shape.
                if (trigger.Contains(position))
                {                  
                    // Mark this entity as being inside a zone for the current frame.
                    // This is used later to detect exit events.
                    allEntitiesInZonesNow.Add(entityPtr);

                    // Attempt to add the entity to the trigger's "currently inside" set.
                    // The .Add() method of a HashSet returns 'true' only if the item was NOT already present.
                    if (trigger.EntitiesInsideLastFrame.Add(entityPtr))
                    {
                        // ON ENTER
                        int realEntityId = i + 1;
                        TriggerR3EventHooks.OnAreaEntered.Raise(new TriggerEventArgs(EventHookPhase.Pre, trigger.Handle, realEntityId));
                    }
                }
            }
        }

        // --- PHASE 2: DETECT EXITS ---
        // Iterate through every trigger zone that exists.
        foreach (TriggerZone trigger in _allTriggers.Values)
        {
            // Use the RemoveWhere method to filter the set in-place.
            // This iterates through all entities that were inside the trigger LAST frame.
            trigger.EntitiesInsideLastFrame.RemoveWhere(entityPtr =>
            {
                // Check if the entity is still inside a zone THIS frame.
                if (allEntitiesInZonesNow.Contains(entityPtr))
                {
                    // The entity is still inside, so we keep it in the set for the next frame.
                    return false; // Keep it, it still inside.s
                }
                else
                {
                    // ON EXIT: The entity was in the set last frame, but is not inside this frame.
                    // This means it has exited the trigger.
                    int realEntityId = GameUnitManagerAPI.Instance.GetUnitArray().GetIndexByAddress((GameUnit*)entityPtr) + 1;
                    TriggerR3EventHooks.OnAreaExited.Raise(new TriggerEventArgs(EventHookPhase.Pre, trigger.Handle, realEntityId));

                    // We must remove it from the set to correctly reflect the new state.
                    return true; // Remove it.
                }
            });
        }
    }
}