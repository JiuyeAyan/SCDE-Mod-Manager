using SHCDESE.EventAPI.Buildings;
using SHCDESE.Lua.DocsGen;

namespace SHCDESE.EventAPI;

/// <summary>
/// Provides static R3EventHook fields for subscribing to various building-related events in the game.
/// </summary>
/// <remarks>
/// This class centralizes all events tied to building actions, such as creation, destruction, damage, and state changes.
/// Modders can subscribe to these hooks to execute custom logic when these game events occur.
/// Each event is fired with a 'Pre' phase (before the original game logic) and a 'Post' phase (after).
/// </remarks>
public static class BuildingR3EventHooks
{
    /// <summary>
    /// Fired when the game checks if a building is being placed adjacent to another of the same type.
    /// </summary>
    /// <remarks>
    /// This event allows for modifying the parameters of the adjacency check or overriding its result.
    /// It is commonly used by the game for buildings like quarries or farms that have placement restrictions based on proximity to others.
    /// In the 'Post' phase, the original function's result is available in <see cref="IsGameBuildingAdjacentToOtherEventArgs.ReturnValue"/>, which can be modified.
    /// </remarks>
    [LuaApiExport("OnIsBuildingAdjacentToOther")]
    public static readonly R3EventHook<IsGameBuildingAdjacentToOtherEventArgs> OnIsBuildingAdjacentToOther = new();

    /// <summary>
    /// Fired just before a Granary spawns a chicken.
    /// </summary>
    /// <remarks>
    /// This event allows for changing the position or the type of "chimp" (typically a chicken) being spawned.
    /// </remarks>
    [LuaApiExport("OnGranarySpawnChicken")]
    public static readonly R3EventHook<GranarySpawnChickenEventArgs> OnGranarySpawnChicken = new();

    /// <summary>
    /// Fired when a Keep is about to spawn a peasant.
    /// </summary>
    /// <remarks>
    /// This event intercepts the peasant spawning logic that originates from the player's Keep.
    /// It allows for modification of the peasant's spawn location and owner, or to cancel the spawn entirely.
    /// The actual unit spawning is handled by the unit manager, and this event acts as a high-level trigger.
    /// </remarks>
    [LuaApiExport("OnKeepSpawnPeasant")]
    public static readonly R3EventHook<KeepSpawnPeasantEventArgs> OnKeepSpawnPeasant = new();

    /// <summary>
    /// Fired when a core building object is spawned into the game world.
    /// </summary>
    /// <remarks>
    /// This is a low-level event that triggers after a building's data structure is initialized but before it's fully integrated.
    /// It's useful for tracking the creation of any building and getting its ID immediately. The building ID is available in the 'Post' phase via <see cref="BuildingSpawnEventArgs.ReturnValue"/>.
    /// This event is triggered by higher-level actions like <see cref="OnBuildStructure"/>.
    /// </remarks>
    [LuaApiExport("OnBuildingSpawn")]
    public static readonly R3EventHook<BuildingSpawnEventArgs> OnBuildingSpawn = new();

    /// <summary>
    /// Fired when a player builds a wall or a tower.
    /// </summary>
    /// <remarks>
    /// This event allows for intercepting the creation of defensive structures. You can modify the start and end coordinates, the owner, or the type of wall being built.
    /// </remarks>
    [LuaApiExport("OnBuildWall")]
    public static readonly R3EventHook<BuildWallEventArgs> OnBuildWall = new();

    /// <summary>
    /// Fired when a building's tile takes damage from any source.
    /// </summary>
    /// <remarks>
    /// This is the primary event for handling building damage. It allows for modifying the amount of damage dealt, redirecting it, or making buildings invincible by setting damage to zero.
    /// The source of the damage (player ID) is also available.
    /// </remarks>
    [LuaApiExport("OnBuildingTileTakeDamage")]
    public static readonly R3EventHook<BuildingTileTakeDamageEventArgs> OnBuildingTileTakeDamage = new();

    /// <summary>
    /// Fired when a player attempts to build a complete structure (prefab), including resource deduction and placement checks.
    /// </summary>
    /// <remarks>
    /// This is a high-level event that represents the entire building placement process. It triggers before <see cref="OnBuildingSpawn"/>.
    /// </remarks>
    [LuaApiExport("OnBuildStructure")]
    public static readonly R3EventHook<BuildStructureEventArgs> OnBuildStructure = new();

    /// <summary>
    /// Fired when a building is bulldozed by the player.
    /// </summary>
    /// <remarks>
    /// This event is specific to the "bulldoze" action, which typically returns resources to the player.
    /// </remarks>
    [LuaApiExport("OnBuildingBulldoze")]
    public static readonly R3EventHook<BuildingBulldozeEventArgs> OnBuildingBulldoze = new();

    /// <summary>
    /// Fired when a building is deleted through a direct, low-level function call.
    /// </summary>
    /// <remarks>
    /// This event is more general than <see cref="OnBuildingBulldoze"/> and can be triggered by game scripts or other internal logic.
    /// It signifies the immediate removal of a building from memory.
    /// </remarks>
    [LuaApiExport("OnBuildingDelete")]
    public static readonly R3EventHook<BuildingDeleteEventArgs> OnBuildingDelete = new();

    /// <summary>
    /// Fired when goods are added to a Goodsyard, Granary, or Armoury.
    /// </summary>
    /// <remarks>
    /// This event allows for direct manipulation of resource flow into storage buildings.
    /// You can change the amount of the good, the type of good, or the target storage building's capacity.
    /// </remarks>
    [LuaApiExport("OnGoodsyardAddGood")]
    public static readonly R3EventHook<AddGoodToGoodsyardEventArgs> OnGoodsyardAddGood = new();

    /// <summary>
    /// Fired when a building's production is paused or unpaused (put to sleep/woken up).
    /// This will get called for every building of a type sequentially.
    /// </summary>
    /// <remarks>
    /// This event captures the toggling of a building's active status. The <see cref="BuildingTogglePauseEventArgs.WasPreviouslySleeping"/> property indicates the state before the toggle.
    /// </remarks>
    [LuaApiExport("OnBuildingTogglePause")]
    public static readonly R3EventHook<BuildingTogglePauseEventArgs> OnTogglePause = new();

    /// <summary>
    /// Fired when a building's production is unpaused (put to sleep/woken up) from the UI.
    /// This will only get called once.
    /// </summary>
    [LuaApiExport("OnBuildingUnpause")]
    public static readonly R3EventHook<BuildingUIUnpauseEventArgs> OnUnpause = new();

    /// <summary>
    /// Fired when a building's production is paused (put to sleep/woken up) from the UI.
    /// This will only get called once.
    /// </summary>
    [LuaApiExport("OnBuildingPause")]
    public static readonly R3EventHook<BuildingUIPauseEventArgs> OnPause = new();

    /// <summary>
    /// Fired when the player changes the production good of a workshop (e.g., Fletcher's Workshop from Bow to Crossbow).
    /// </summary>
    /// <remarks>
    /// This allows for intercepting and changing the selected good for production buildings that have multiple output options.
    /// </remarks>
    [LuaApiExport("OnSwitchProductionGood")]
    public static readonly R3EventHook<BuildingSwitchProductionGoodEventArgs> OnSwitchProductionGood = new();

    /// <summary>
    /// Fired during the validation phase of building placement, before any resources are spent.
    /// </summary>
    /// <remarks>
    /// This is a powerful event that allows for the creation of custom building placement rules.
    /// By setting <see cref="BuildingPlacementValidationEventArgs.CustomValidationRules"/> to true, you can use <see cref="BuildingPlacementValidationEventArgs.ForceBlockPlacementState"/>
    /// to either allow or deny the placement, overriding the game's default logic entirely.
    /// </remarks>
    [LuaApiExport("OnPlacementValidation")]
    public static readonly R3EventHook<BuildingPlacementValidationEventArgs> OnPlacementValidation = new();

    /// <summary>
    /// Fired when a building is refunded as a result of a player self-bulldoze.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The final refunded resource amounts are determined by two compounding factors:
    /// <list type="number">
    ///   <item><description>
    ///     <b>Percentage</b> — passed into the underlying <c>c_game_refund_building</c> native function.
    ///     Modifying <see cref="BuildingRefundEventArgs.Percentage"/> in the <see cref="EventHookPhase.Pre"/>
    ///     phase will change the percentage forwarded to the native function.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Per-resource multipliers</b> — applied by a secondary inline intercept after the native
    ///     function computes its base amounts. These are controlled by
    ///     <see cref="API.GameBuildingManagerAPI.WoodRefundMultiplier"/>,
    ///     <see cref="API.GameBuildingManagerAPI.StoneRefundMultiplier"/>,
    ///     <see cref="API.GameBuildingManagerAPI.IronRefundMultiplier"/>,
    ///     <see cref="API.GameBuildingManagerAPI.PitchRefundMultiplier"/>,
    ///     <see cref="API.GameBuildingManagerAPI.GoldRefundMultiplier"/>,
    ///     which each default to <c>DEFAULT_BUILDING_REFUND_MULTIPLIER</c> (0.5).
    ///   </description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The effective refund for each resource is therefore:
    /// <c>buildingCost * (Percentage / 100f) * ResourceRefundMultiplier</c>.
    /// </para>
    /// </remarks>
    [LuaApiExport("OnBuildingRefund")]
    public static readonly R3EventHook<BuildingRefundEventArgs> OnBuildingRefund = new();

    /// <summary>
    /// Fired when walls are bulldozed.
    /// </summary>
    [LuaApiExport("OnWallBulldoze")]
    public static readonly R3EventHook<WallBulldozeEventArgs> OnWallBulldoze = new();

    /// <summary>
    /// Fired when a building is repaired
    /// </summary>
    [LuaApiExport("OnBuildingRepair")]
    public static readonly R3EventHook<BuildingRepairEventArgs> OnBuildingRepair = new();

    /// <summary>
    /// Fired when the game queries whether to allow a building repair in a certain position.
    /// A failed query would be caused by enemy units near the proximity, for example.
    /// </summary>
    [LuaApiExport("OnBuildingAllowRepairInProximity")]
    public static readonly R3EventHook<BuildingAllowRepairInProximityEventArgs> OnBuildingAllowRepairInProximity = new();

    /// <summary>
    /// Fired when the game calculates the stone amount that a repair for a structure would cost.
    /// </summary>
    [LuaApiExport("OnBuildingCalculateStoneRepairCost")]
    public static readonly R3EventHook<BuildingCalculateStoneRepairCostEventArgs> OnBuildingCalculateStoneRepairCost = new();

    /// <summary>
    /// Fired when the game calculates the wood amount that a repair for a structure would cost.
    /// </summary>
    [LuaApiExport("OnBuildingCalculateWoodRepairCost")]
    public static readonly R3EventHook<BuildingCalculateWoodRepairCostEventArgs> OnBuildingCalculateWoodRepairCost = new();

    /// <summary>
    /// Fired when any player builds a pitch ditch.
    /// </summary>
    [LuaApiExport("OnBuildPitchDitch")]
    public static readonly R3EventHook<BuildPitchDitchEventArgs> OnBuildPitchDitch = new();

    /// <summary>
    /// Fired when any pitch ditch is removed.
    /// </summary>
    [LuaApiExport("OnRemovePitchDitch")]
    public static readonly R3EventHook<RemovePitchDitchEventArgs> OnRemovePitchDitch = new();

    /// <summary>
    /// Fired when the game evaluates whether to close a gatehouse or not
    /// </summary>
    [LuaApiExport("OnGatehouseQuery")]
    public static readonly R3EventHook<GatehouseQueryEventArgs> OnGatehouseQuery = new();
}