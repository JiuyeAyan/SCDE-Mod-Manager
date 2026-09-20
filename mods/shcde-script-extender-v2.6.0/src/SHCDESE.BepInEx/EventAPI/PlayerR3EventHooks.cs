using SHCDESE.EventAPI.Player;
using SHCDESE.Lua.DocsGen;

namespace SHCDESE.EventAPI;

/// <summary>
/// Provides static R3EventHook fields for subscribing to player-specific resource and market events.
/// </summary>
public static class PlayerR3EventHooks
{
    /// <summary>
    /// Fired when a player toggles their ingame menu.
    /// </summary>
    [LuaApiExport("OnToggleIngameMenuVisibility")]
    public static readonly R3EventHook<PlayerToggleIngameMenuVisibilityEventArgs> OnToggleIngameMenuVisibility = new();

    /// <summary>
    /// Fired when a resource is added to a player's global stockpiles.
    /// </summary>
    [LuaApiExport("OnPlayerAddResource")]
    public static readonly R3EventHook<PlayerAddResourceEventArgs> OnPlayerAddResource = new();

    /// <summary>
    /// Fired when a resource is subtracted from a player's global stockpiles, for instance, due to building costs.
    /// </summary>
    [LuaApiExport("OnPlayerSubtractResource")]
    public static readonly R3EventHook<PlayerSubtractResourceEventArgs> OnPlayerSubtractResource = new();

    /// <summary>
    /// Fired when a player buys or sells a good at the market.
    /// </summary>
    [LuaApiExport("OnPlayerMarketInteraction")]
    public static readonly R3EventHook<PlayerMarketInteractionEventArgs> OnPlayerMarketInteraction = new();

    /// <summary>
    /// Fired every time the game processes a player's food consumption
    /// </summary>
    [LuaApiExport("OnPlayerFoodTick")]
    public static readonly R3EventHook<PlayerFoodConsumptionTickEventArgs> OnPlayerFoodConsumptionTick = new();

    /// <summary>
    /// Fired every time the game calculates the drunk coverage of a player.
    /// Default formula is: abs(3000 * working_inns_num / total_population) for each individual player.
    /// </summary>
    [LuaApiExport("OnPlayerCalculateDrunkPercentage")]
    public static readonly R3EventHook<PlayerCalculateDrunkPercentageEventArgs> OnPlayerCalculateDrunkPercentage = new();

    /// <summary>
    /// Fired every time a AI player evaluates whether to help a player via defend order.
    /// </summary>
    [LuaApiExport("OnPlayerAIEvaluateDefendOrder")]
    public static readonly R3EventHook<PlayerAIEvaluateDefendOrder> OnPlayerAIEvaluateDefendOrder = new();

    /// <summary>
    /// Fired every time a AI player evaluates whether to help a player via attack order.
    /// </summary>
    [LuaApiExport("OnPlayerAIEvaluateAttackOrder")]
    public static readonly R3EventHook<PlayerAIEvaluateAttackOrder> OnPlayerAIEvaluateAttackOrder = new();

    /// <summary>
    /// Fired every time a AI player evaluates whether it -can- send goods to a player.
    /// </summary>
    [LuaApiExport("OnPlayerAIEvaluateCanSpareGoods")]
    public static readonly R3EventHook<PlayerAIEvaluateCanSpareGoods> OnPlayerAIEvaluateCanSpareGoods = new();

    /// <summary>
    /// Fired every time a AI player sends good to a player
    /// </summary>
    [LuaApiExport("OnPlayerAIRequestGoods")]
    public static readonly R3EventHook<PlayerAIRequestGoodsEventArgs> OnPlayerAIRequestGoods = new();

    /// <summary>
    /// Fired every time a AI player receives goods from a player
    /// </summary>
    [LuaApiExport("OnPlayerAIRequestReceivedGoods")]
    public static readonly R3EventHook<PlayerAIRequestReceivedGoodsEventArg> OnPlayerAIRequestReceivedGoods = new();

    /// <summary>
    /// Fired every game tick for each active player when their tax income contribution is calculated
    /// and added to a monthly accumulator. This fires many times
    /// per month, use <see cref="OnPlayerTaxIncome"/> if you only care about the final settled amount.
    /// </summary>
    [LuaApiExport("OnPlayerCalculateTaxes")]
    public static readonly R3EventHook<PlayerCalculateTaxesEventArgs> OnPlayerCalculateTaxes = new();

    /// <summary>
    /// Fired every game tick for each active player when their donation/bribe expenditure is calculated
    /// and added to a monthly accumulator. Only fires when
    /// <see cref="Interop.Enums.TaxesMode"/> is <c>HugeDonation</c>, <c>BigDonation</c>, or <c>SmallDonation</c>.
    /// This fires many times per month, the gold is not deducted yet at this point.
    /// </summary>
    [LuaApiExport("OnPlayerCalculateExpenditure")]
    public static readonly R3EventHook<PlayerCalculateExpenditureEventArgs> OnPlayerCalculateExpenditure = new();

    /// <summary>
    /// Fired once per month per active player during the settlement tick, after the accumulated
    /// tax income and donation expenses have been normalized (/10) and applied to the player's gold
    /// (<c>r_TotalGoodsGold</c>). At this point the gold has already moved, income has been added
    /// and expenses have been subtracted (floored at zero). Provides the final settled income amount,
    /// expense amount, and the player's resulting gold total.
    /// </summary>
    [LuaApiExport("OnPlayerTaxIncome")]
    public static readonly R3EventHook<PlayerTaxIncomeEventArgs> OnPlayerTaxIncome = new();

    /// <summary>
    /// Same as <see cref="OnPlayerTaxIncome"/> but for expenditures (bribes) only
    /// </summary>
    [LuaApiExport("OnPlayerTaxExpenditure")]
    public static readonly R3EventHook<PlayerTaxExpenditureEventArgs> OnPlayerTaxExpenditure = new();

    /// <summary>
    /// Called just before the final popularity score is calculated for a player.
    /// </summary>
    [LuaApiExport("OnPlayerCalculatePopularity")]
    public static readonly R3EventHook<PlayerCalculatePopularityEventArgs> OnPlayerCalculatePopularity = new();
}