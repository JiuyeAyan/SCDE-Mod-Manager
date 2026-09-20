using RedBird.Core.Memory;
using SHCDESE.Detours;
using SHCDESE.ManagedHooks;
using SHCDESE.ManagedHooks.ILHooks;
using System;
using System.Collections.Generic;

namespace SHCDESE.NativeHooks;

internal class DetourManager
{
    private static readonly Lazy<DetourManager> _lazy = new(() => new DetourManager());
    public static DetourManager Instance => _lazy.Value;

    private DetourManager()
    {
        nativeDetours = new List<object>();
    }

    internal List<object> nativeDetours;

    internal void ApplyNative(ReadOnlySpan<byte> memory, ScanRegion region)
    {
        nativeDetours.AddRange([
            new BulkTribeDetours(memory, region),
            new BulkUnitDetours(memory, region),
            new BulkBuildingDetours(memory, region),
            new BulkVegetationDetours(memory, region),
            new BulkMapLoaderDetours(memory, region),
            new BulkProjectileDetours(memory, region),
            new BulkPlayerDetours(memory, region),
            new BulkSoundDetours(memory, region),
            new BulkTileDetours(memory, region),
            new BulkTimeDetours(memory, region),
            new BulkMapEditorDetours(memory, region),
            new BulkAIDetours(memory, region),
            new BulkChoreDetours(memory, region),
            new BulkPathingDetours(memory, region),
            new BulkEngineDetours(memory, region),
            new BulkVEHDetours(memory, region)
    ]);
    }

    internal void ApplyManaged()
    {
        ILHooksManager.Instance.Apply();
        ManagedHookManager.Instance.Apply();
    }

    internal void ApplyManagedEarly()
    {
        ILHooksManager.Instance.ApplyEarly();
        ManagedHookManager.Instance.ApplyEarly();
    }
}
