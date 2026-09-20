using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Runtime.CompilerServices;

namespace SHCDESE.Extensions;

/// <summary>
/// Provides high-performance extension methods for direct manipulation of the <see cref="GameBuilding"/> struct.
/// </summary>
public static class BuildingExtensions
{
    /// <summary>
    /// Gets the amount of a specific good in the building's local storage using high-performance pointer access.
    /// </summary>
    /// <param name="self">The <see cref="GameBuilding"/> instance to query.</param>
    /// <param name="good">The type of good to retrieve the amount for.</param>
    /// <returns>The current amount of the specified good in local storage.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe static int GetLocalGoodsAmount(this ref readonly GameBuilding self, eGoods good)
    {
        int* localStorage = (int*)Unsafe.AsPointer(ref Unsafe.AsRef(in self.r_NullAmount));
        return localStorage[(int)good];
    }

    /// <summary>
    /// Adds a specified amount of a good to the building's local storage, clamping at the given capacity.
    /// </summary>
    /// <param name="self">The <see cref="GameBuilding"/> instance to modify.</param>
    /// <param name="good">The type of good to add.</param>
    /// <param name="amount">The amount to add.</param>
    /// <param name="capacity">Optional capacity override. If null, the building's <code>r_MaxGoodStackAmount</code> is used.</param>
    /// <remarks>
    /// This method updates both the specific good's amount in the local storage array and syncs the <code>r_CurrentGoodStackAmount</code>.
    /// It will also set the <code>r_LocalStorageGoodType</code> if the new amount is greater than zero.
    /// This function does not visually update.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe static void AddLocalGoodsAmount(this ref GameBuilding self, eGoods good, int amount, int? capacity)
    {
        capacity ??= (int)self.r_MaxGoodStackAmount;

        int* localStorage = (int*)Unsafe.AsPointer(ref Unsafe.AsRef(in self.r_NullAmount));
        int newValue = Math.Min(localStorage[(int)good] + amount, (int)capacity);
        localStorage[(int)good] = newValue;
        self.r_CurrentGoodStackAmount = (uint)newValue;

        if (newValue > 0)
        {
            self.r_LocalStorageGoodType = good;
        }
    }

    /// <summary>
    /// Removes a specified amount of a good from the building's local storage by adding a negative value.
    /// </summary>
    /// <param name="self">The <see cref="GameBuilding"/> instance to modify.</param>
    /// <param name="good">The type of good to remove.</param>
    /// <param name="amount">The amount to remove. This value is expected to be negative. For example, to remove 10, pass -10.</param>
    /// <remarks>
    /// The final amount is clamped at zero. This method updates the <code>r_CurrentGoodStackAmount</code> and resets
    /// the <code>r_LocalStorageGoodType</code> to <see cref="eGoods.STORED_NULL"/> if the storage becomes empty.
    /// This function does not visually update.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe static void RemoveLocalGoodsAmount(this ref GameBuilding self, eGoods good, int amount)
    {
        int* localStorage = (int*)Unsafe.AsPointer(ref Unsafe.AsRef(in self.r_NullAmount));
        int newValue = Math.Max(localStorage[(int)good] + amount, 0);
        localStorage[(int)good] = newValue;
        self.r_CurrentGoodStackAmount = (uint)newValue;

        if (newValue == 0)
        {
            self.r_LocalStorageGoodType = eGoods.STORED_NULL;
        }
    }

    /// <summary>
    /// Updates the visual resources for the specified game building, ensuring that its appearance reflects the current
    /// state of goodsyard resources.
    /// </summary>
    /// <param name="self">A reference to the game building whose visual resources are to be updated.</param>
    /// <remarks>
    /// This method should be called after changes to the building's goods or yard state to refresh
    /// its visual representation.
    /// </remarks>
    public unsafe static void UpdateLocalGoodsResourceVisuals(this ref GameBuilding self)
    {
        GameBuilding* ptr = (GameBuilding*)Unsafe.AsPointer(ref Unsafe.AsRef(in self));

        int buildingId = GameBuildingManagerAPI.Instance.GetBuildingsArray().GetIndexByAddress(ptr) + 1;
        GameBuildingManagerAPI.Instance.UpdateVisualResourceGoods(buildingId);
        GameTileManagerAPI.Instance.TryUpdateTileResourceVisualsForBuilding(buildingId);
    }
}
