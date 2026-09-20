using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// Defines the taxation levels available to a player, ranging from population bribes (donations)
/// to increasingly harsh tax rates. Values below 3 generate gold expenditure; values above 3
/// generate gold income. The exact gold amount scales with (taxable) population each game tick
/// and is settled periodically. Popularity effects are applied separately and not part of this enum's logic.
/// </summary>
public enum TaxesMode : UInt32
{
    /// <summary>
    /// Huge donation: bribes the population with a expense multiplier of (5-0)/2 = 2.5x taxable population per tick.
    /// Grants the highest popularity bonus. Only applies if the player can afford it.
    /// </summary>
    HugeDonation = 0,

    /// <summary>
    /// Big donation: bribes the population with an expense multiplier of (5-1)/2 = 2.0x population per tick.
    /// Grants a large popularity bonus. Only applies if the player can afford it.
    /// </summary>
    BigDonation = 1,

    /// <summary>
    /// Small donation: bribes the population with an expense multiplier of (5-2)/2 = 1.5x population per tick.
    /// Grants a small popularity bonus. Only applies if the player can afford it.
    /// </summary>
    SmallDonation = 2,

    /// <summary>
    /// No taxes and no bribes. Neither c_game_calculate_donation_expense nor
    /// <see cref="Detours.BulkPlayerDetours.c_game_calculate_tax_income_hook_impl"/> is called. Net gold effect is zero each tick.
    /// Grants a minor popularity bonus.
    /// </summary>
    None = 3,

    /// <summary>
    /// Low tax rate: income multiplier of (4-1)/2 = 1.5x taxable population per tick.
    /// Minor popularity penalty. May receive a bonus multiplier based on <see cref="AIAdvantage"/>.
    /// </summary>
    Low = 4,

    /// <summary>
    /// Moderate tax rate: income multiplier of (5-1)/2 = 2.0x taxable population per tick.
    /// Moderate popularity penalty. May receive a bonus multiplier based on <see cref="AIAdvantage"/>.
    /// </summary>
    Moderate = 5,

    /// <summary>
    /// High tax rate: income multiplier of (6-1)/2 = 2.5x taxable population per tick.
    /// Significant popularity penalty. May receive a bonus multiplier based on <see cref="AIAdvantage"/>.
    /// </summary>
    High = 6,

    /// <summary>
    /// Mean tax rate: income multiplier of (7-1)/2 = 3.0x taxable population per tick.
    /// Heavy popularity penalty. May receive a bonus multiplier based on <see cref="AIAdvantage"/>.
    /// </summary>
    Mean = 7,

    /// <summary>
    /// Usurious tax rate: income multiplier of (8-1)/2 = 3.5x taxable population per tick.
    /// Severe popularity penalty. May receive a bonus multiplier based on <see cref="AIAdvantage"/>.
    /// </summary>
    Usurious = 8,

    /// <summary>
    /// Cruel tax rate: income multiplier of (9-1)/2 = 4.0x taxable population per tick.
    /// Very severe popularity penalty. May receive a bonus multiplier based on <see cref="AIAdvantage"/>.
    /// </summary>
    Cruel = 9,

    /// <summary>
    /// Most cruel tax rate: income multiplier of (10-1)/2 = 4.5x taxable population per tick.
    /// Extreme popularity penalty. May receive a bonus multiplier based on <see cref="AIAdvantage"/>.
    /// </summary>
    MostCruel = 10,

    /// <summary>
    /// Cruellest tax rate: income multiplier of (11-1)/2 = 5.0x taxable population per tick.
    /// Maximum popularity penalty. May receive a bonus multiplier based on <see cref="AIAdvantage"/>.
    /// </summary>
    Cruellest = 11
}