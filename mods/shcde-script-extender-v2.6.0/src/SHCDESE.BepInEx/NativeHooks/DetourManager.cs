using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.Core.Memory;
using RedBird.Core.Memory.Scanners;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Memory.Scanners;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.Detours;
using SHCDESE.Interop;
using SHCDESE.Logging;
using SHCDESE.ManagedHooks;
using SHCDESE.ManagedHooks.ILHooks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SHCDESE.NativeHooks;

internal class DetourManager
{
    private static readonly Lazy<DetourManager> _lazy = new(() => new DetourManager());
    public static DetourManager Instance => _lazy.Value;
    private HookTransaction? _tx;

    private DetourManager()
    {
        nativeDetours = new List<object>();
    }

    internal List<object> nativeDetours;

    internal void ApplyNative(ReadOnlySpan<byte> memory, ScanRegion region, DataScanner scanner, AobCacheOptions cacheOptions)
    {
        NativeDetourBackend backend = new(new NativeDetourOptions() { SuspendThreadsDuringPatch = false });
        _tx ??= new HookTransaction(region, Plugin.Instance.LoggerFactory, new HookTransactionOptions()
        {
            FailureMode = TransactionFailureMode.ContinueOnError, // technically a bad idea, but whatever.
            AobCache = cacheOptions,
            Backend = backend
        });

        CommitResult? setupResult = null;
        try
        {
            nativeDetours.AddRange([
                new BulkMonoDetours(cacheOptions),
                new BulkTribeDetours(memory, region, _tx, scanner),
                new BulkUnitDetours(memory, region, _tx, scanner),
                new BulkBuildingDetours(memory, region, _tx, scanner),
                new BulkVegetationDetours(memory, region, _tx, scanner),
                new BulkMapLoaderDetours(memory, region, _tx, scanner),
                new BulkProjectileDetours(memory, region, _tx, scanner),
                new BulkPlayerDetours(memory, region, _tx, scanner),
                new BulkSoundDetours(memory, region, _tx, scanner),
                new BulkTileDetours(memory, region, _tx, scanner),
                new BulkTimeDetours(memory, region, _tx, scanner),
                new BulkMapEditorDetours(memory, region, _tx, scanner),
                new BulkAIDetours(memory, region, _tx, scanner),
                new BulkChoreDetours(memory, region, _tx, scanner),
                new BulkPathingDetours(memory, region, _tx, scanner),
                new BulkEngineDetours(memory, region, _tx, scanner),
                new BulkVEHDetours(memory, region, _tx, scanner)
            ]);

            setupResult = _tx.Commit();
        } 
        catch (Exception ex)
        {
            LogHelper.Fatal(ex, "Error during hook installation!");
        } 
        finally
        {
            if (setupResult != null && setupResult.IsCompleteSuccess)
                LogHelper.Information($"Hook setup completed in {setupResult.TotalMilliseconds}ms");
        }
       
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
