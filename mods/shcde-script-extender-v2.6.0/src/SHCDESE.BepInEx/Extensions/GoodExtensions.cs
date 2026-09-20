using SHCDESE.Interop;
using System.Runtime.CompilerServices;

namespace SHCDESE.Extensions;

/// <summary>
/// Provides high-performance extension methods for the <see cref="eGoods"/> enumeration to categorize goods.
/// </summary>
public static class GoodExtensions
{
    /// <summary>
    /// Checks if the good is a type of food that is stored in the Granary.
    /// </summary>
    /// <param name="self">The <see cref="eGoods"/> instance to check.</param>
    /// <returns><c>true</c> if the good is a food type (Bread, Cheese, Meat, or Fruit); otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsGranaryFood(this eGoods self)
    {
        return self is >= eGoods.STORED_FOOD_BREAD and <= eGoods.STORED_FOOD_FRUIT;
    }

    /// <summary>
    /// Checks if the good is a type of raw material or resource that is stored in the Stockpile (Goodsyard).
    /// </summary>
    /// <param name="self">The <see cref="eGoods"/> instance to check.</param>
    /// <returns><c>true</c> if the good is a Stockpile resource (Wood, Hops, Stone, Iron, Pitch, Wheat, or Flour); otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsGoodsyardGood(this eGoods self)
    {
        switch (self)
        {
            case eGoods.STORED_WOOD_PLANKS:
            case eGoods.STORED_RAW_HOPS:
            case eGoods.STORED_STONE_BLOCKS:
            case eGoods.STORED_IRON_INGOTS:
            case eGoods.STORED_PITCH_RAW:
            case eGoods.STORED_RAW_WHEAT:
            case eGoods.STORED_FLOUR:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks if the good is a type of weapon or armor that is stored in the Armoury.
    /// </summary>
    /// <param name="self">The <see cref="eGoods"/> instance to check.</param>
    /// <returns><c>true</c> if the good is a weapon or armor type; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsArmouryGood(this eGoods self)
    {
        return self is >= eGoods.STORED_BOWS and <= eGoods.STORED_METAL_ARMOUR;
    }

    /// <summary>
    /// Checks if a eGoods is the same as a eGoods32
    /// </summary>
    /// <param name="self">The eGoods</param>
    /// <param name="other">The eGoods32</param>
    /// <returns>Equal or not</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSameGood(this eGoods self, eGoods32 other)
    {
        return (int)self == (int)other;
    }

    /// <summary>
    /// Checks if a eGoods32 is the same as a eGoods
    /// </summary>
    /// <param name="self">The eGoods32</param>
    /// <param name="other">The eGoods</param>
    /// <returns>Equal or not</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSameGood(this eGoods32 self, eGoods other)
    {
        return (int)self == (int)other;
    }

    /// <summary>
    /// Converts a eGoods to a eGoods32 for internal purposes
    /// </summary>
    /// <param name="self">eGoods</param>
    /// <returns>eGoods32 representation</returns>
    public static eGoods32 To32(this eGoods self) => (eGoods32)(int)self;

    /// <summary>
    /// Converts a eGoods32 to a eGoods for internal purposes
    /// </summary>
    /// <param name="self">eGoods</param>
    /// <returns>eGoods32 representation</returns>
    public static eGoods To16(this eGoods32 self) => (eGoods)(int)self;
}