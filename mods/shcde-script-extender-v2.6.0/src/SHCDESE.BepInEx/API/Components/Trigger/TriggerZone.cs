using SHCDESE.Interop;
using System;
using System.Collections.Generic;

namespace SHCDESE.API.Components.Trigger;

public abstract class TriggerZone
{
    public int Handle { get; }
    public Func<GameUnit, bool>? UnitFilter { get; set; } // Optional filter, e.g., only player 1
    public Func<GameProjectile, bool>? ProjectileFilter { get; set; } // Optional filter

    // State management: remembers who was inside last frame.
    internal HashSet<UInt64> EntitiesInsideLastFrame { get; } = new HashSet<UInt64>();

    protected TriggerZone(int handle) 
    {
        Handle = handle; 
    }

    // Abstract method to be implemented by Rect, Circle, etc.
    public abstract bool Contains(UnmanagedVector2<UInt16> position);
}
