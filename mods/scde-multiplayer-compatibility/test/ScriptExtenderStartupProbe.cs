// Test-copy only. This probe is not compiled into the release package.
using System;
using System.Diagnostics;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;

[BepInPlugin("scdemm.test.se-startup", "SE Startup Test Probe", "1.0.0")]
[BepInDependency("com.jiuyeayan.scde.multiplayer-compatibility")]
public sealed class ScriptExtenderStartupProbe : BaseUnityPlugin
{
    private void Awake()
    {
        try
        {
            var checker = Chainloader.PluginInfos["com.jiuyeayan.scde.multiplayer-compatibility"].Instance;
            var refresh = checker.GetType().GetMethod("RefreshRuntimeProfile", BindingFlags.NonPublic | BindingFlags.Instance);
            bool ready = (bool)refresh.Invoke(checker, null);
            var se = Chainloader.PluginInfos["000shcdese"].Instance;
            var updater = se.GetType().Assembly.GetType("SHCDESE.API.Components.Archive.MapModManager");
            var method = updater.GetMethod("TryUpdateModsFromRemote", BindingFlags.NonPublic | BindingFlags.Instance);
            var patches = Harmony.GetPatchInfo(method);
            bool blocked = false;
            if (patches != null) foreach (var prefix in patches.Prefixes)
                if (prefix.owner == "com.jiuyeayan.scde.multiplayer-compatibility") blocked = true;
            if (!ready || !blocked) throw new Exception("Runtime profile/policy was not ready.");
            Logger.LogInfo("SE_LIVE_BRIDGE_OK runtimeProfile=true managedPolicyPrefix=true");
            string map = Environment.GetEnvironmentVariable("SCDE_SE_PROBE_MAP");
            if (!String.IsNullOrEmpty(map))
            {
                var hooks = se.GetType().Assembly.GetType("SHCDESE.ManagedHooks.ManagedHookManager", true);
                var update = hooks.GetMethod("MapFileManager_UpdateWorkshopMap_Hook", BindingFlags.NonPublic | BindingFlags.Instance);
                var owner = hooks.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
                var clock = Stopwatch.StartNew();
                // A plugin map is excluded before the MapFileManager instance is used, in both paths.
                update.Invoke(owner, new object[] { null, map, map });
                Logger.LogInfo("SE_LIVE_WORKSHOP_PROBE_OK elapsedMs=" + clock.Elapsed.TotalMilliseconds);
            }
        }
        catch (Exception error) { Logger.LogError("SE_LIVE_BRIDGE_FAILED " + error); }
    }
}
