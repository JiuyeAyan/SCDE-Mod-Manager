using System;
using UnityEngine;

namespace SHCDESE.EventAPI.Units;
public class UnitUnityVisualSpawnEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }
    public GameObject GameObject { get; set; }
    public SpriteRenderer SpriteRenderer { get; set; }

    public UnitUnityVisualSpawnEventArgs(EventHookPhase phase, Int32 unitId, GameObject gameObject, SpriteRenderer spriteRenderer)
    {
        Phase = phase;
        UnitId = unitId;
        GameObject = gameObject;
        SpriteRenderer = spriteRenderer;
    }
}
