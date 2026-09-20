using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// Rarely used enum type for identifying granary food types.
/// Used in c_game_handle_player_food_restriction_for_chore
/// </summary>
public enum GranaryFoodType : Int32
{
    Bread = 0,
    Cheese = 1,
    Meat = 2,
    Fruit = 3
}
