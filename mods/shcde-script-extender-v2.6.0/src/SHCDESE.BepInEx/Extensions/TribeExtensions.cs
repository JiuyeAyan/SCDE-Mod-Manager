using SHCDESE.Interop;
using System;
using System.Runtime.CompilerServices;
using SHCDESE.Logging;
using RedBird.Core.Memory;
namespace SHCDESE.Extensions;

public unsafe static class TribeExtensions
{

    /// <summary>
    /// Sets a last ranged attacker globalId by index. If index is invalid it defaults to the first entry.
    /// </summary>
    /// <param name="self">The tribe to access</param>
    /// <param name="index">The index of the last ranged attacker (to-be)</param>
    /// <param name="globalId">The globalId of the attacker</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetTrackedRangedAttackerIndex(this NativePointer<GameTribe> self, int index, UInt16 globalId)
    {
        switch (index)
        {
            case 0: 
                self.Pointer->r_LastRangedAttackerGlobalId1 = globalId; 
                break;
            case 1:
                self.Pointer->r_LastRangedAttackerGlobalId2 = globalId; 
                break;
            case 2:
                self.Pointer->r_LastRangedAttackerGlobalId3 = globalId; 
                break;
            case 3: 
                self.Pointer->r_LastRangedAttackerGlobalId4 = globalId; 
                break;
            case 4: 
                self.Pointer->r_LastRangedAttackerGlobalId5 = globalId; 
                break;
            case 5: 
                self.Pointer->r_LastRangedAttackerGlobalId6 = globalId; 
                break;
            case 6: 
                self.Pointer->r_LastRangedAttackerGlobalId7 = globalId; 
                break;
            case 7: 
                self.Pointer->r_LastRangedAttackerGlobalId8 = globalId; 
                break;
            case 8: 
                self.Pointer->r_LastRangedAttackerGlobalId9 = globalId; 
                break;
            case 9: 
                self.Pointer->r_LastRangedAttackerGlobalId10 = globalId; 
                break;
            default: 
                self.Pointer->r_LastRangedAttackerGlobalId1 = globalId; 
                break;
        }
    }

    /// <summary>
    /// Gets a patrol point by index (0-based)
    /// </summary>
    /// <param name="self">The tribe to access</param>
    /// <param name="index">The index of the specified patrol point</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnmanagedVector2<UInt16> GetPatrolPoint(this NativePointer<GameTribe> self, int index)
    {
        if (index > 9)
        {
            LogHelper.Error($"Requested patrol point out of range: {index}");
            return default;
        }
        UnmanagedVector2<UInt16>* arr = (UnmanagedVector2<UInt16>*)&self.Pointer->r_PatrolPoint1TileX;
        return arr[index];
    }

    /// <summary>
    /// Sets a patrol point by index (0-based)
    /// </summary>
    /// <param name="self">The tribe to access</param>
    /// <param name="index">The index of the specified patrol point</param>
    /// <param name="point">The x, y data</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetPatrolPoint(this NativePointer<GameTribe> self, int index, UnmanagedVector2<UInt16> point)
    {
        if (index > 9)
        {
            LogHelper.Error($"Requested patrol point out of range: {index}");
            return;
        }
        UnmanagedVector2<UInt16>* arr = (UnmanagedVector2<UInt16>*)&self.Pointer->r_PatrolPoint1TileX;
        arr[index] = point;

    }
}
