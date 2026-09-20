using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace JiuyeAyan.SCDEMultiplayerCompatibility
{
    // Read the public registry; preserve SE's lobby filters, member checks and anti-tamper.
    internal static class ScriptExtenderBridge
    {
        internal const string PackageId = "shcde-script-extender";
        internal const string PluginId = "000shcdese";
        private static PropertyInfo registryInstance;
        private static MethodInfo registeredDirectories;
        private static bool managedUpdaterBlocked;

        internal static void InstallManagedPolicy(Harmony harmony)
        {
            PluginInfo extender;
            if (!Chainloader.PluginInfos.TryGetValue(PluginId, out extender) || ReferenceEquals(extender.Instance, null)) return;
            Type updater = extender.Instance.GetType().Assembly.GetType("SHCDESE.API.Components.Archive.MapModManager", true);
            MethodInfo automaticUpdate = updater.GetMethod("TryUpdateModsFromRemote", BindingFlags.NonPublic | BindingFlags.Instance);
            if (automaticUpdate == null) throw new MissingMethodException("Unsupported Script Extender updater API.");
            harmony.Patch(automaticUpdate, prefix: new HarmonyMethod(typeof(ScriptExtenderBridge), "BlockAutomaticDeployment"));
            managedUpdaterBlocked = true;
            SCDEMultiplayerCompatibilityPlugin.LogSettingsGuardInfo("SE_MANAGED_POLICY: automatic Workshop install/update/unsubscribe/restart disabled; existing SE Mods still load.");
            InstallWorkshopMetadataFastPath(harmony, extender.Instance.GetType().Assembly);
        }

        private static bool BlockAutomaticDeployment() { return false; }

        private static void InstallWorkshopMetadataFastPath(Harmony harmony, Assembly extender)
        {
            if (extender.GetName().Version != new Version(2, 6, 0, 0)) return;
            try
            {
                Type hooks = extender.GetType("SHCDESE.ManagedHooks.ManagedHookManager", true);
                MethodInfo update = hooks.GetMethod("MapFileManager_UpdateWorkshopMap_Hook", BindingFlags.NonPublic | BindingFlags.Instance);
                if (update == null) throw new MissingMethodException("SE Workshop hook not found.");
                SeWorkshopMetadata.Initialize(Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(extender.Location), "ICSharpCode.SharpZipLib.dll")));
                harmony.Patch(update, prefix: new HarmonyMethod(typeof(ScriptExtenderBridge), "SkipPluginMapRecompression"));
                SCDEMultiplayerCompatibilityPlugin.LogSettingsGuardInfo("SE_WORKSHOP_METADATA_FAST_PATH_READY: plugin maps require only info.json, ordinary maps retain SE handling.");
            }
            catch (Exception error)
            {
                SCDEMultiplayerCompatibilityPlugin.LogSettingsGuardInfo("SE_WORKSHOP_METADATA_FAST_PATH_UNAVAILABLE: retaining upstream handling. " + error.Message);
            }
        }

        private static bool SkipPluginMapRecompression(string file)
        {
            if (!SeWorkshopMetadata.IsPluginMap(file)) return true;
            SCDEMultiplayerCompatibilityPlugin.LogSettingsGuardInfo("SE_WORKSHOP_PLUGIN_MAP_SKIPPED: " + Path.GetFileName(file));
            return false;
        }

        internal static List<RuntimeMod> Read(bool required)
        {
            var mods = new Dictionary<string, RuntimeMod>(StringComparer.OrdinalIgnoreCase);
            PluginInfo extender;
            bool loaded = Chainloader.PluginInfos.TryGetValue(PluginId, out extender) &&
                          !ReferenceEquals(extender.Instance, null);
            if (required && !loaded) throw new InvalidOperationException("Script Extender did not load.");
            if (required && !managedUpdaterBlocked) throw new InvalidOperationException("Script Extender managed deployment policy is not active.");
            if (loaded)
            {
                if (registryInstance == null)
                {
                    Type registry = extender.Instance.GetType().Assembly.GetType(
                        "SHCDESE.API.Components.ModManager.GameAssetModManager", true);
                    registryInstance = registry.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    registeredDirectories = registry.GetMethod("GetRegisteredAssetDirectories", Type.EmptyTypes);
                    if (registryInstance == null || registeredDirectories == null)
                        throw new MissingMemberException("Unsupported Script Extender registry API.");
                }
                object registryObject = registryInstance.GetValue(null, null);
                var entries = (IEnumerable)registeredDirectories.Invoke(registryObject, null);
                foreach (object entry in entries)
                {
                    object info = entry.GetType().GetProperty("Key").GetValue(entry, null);
                    Type type = info.GetType();
                    string id = (string)type.GetProperty("GUID").GetValue(info, null);
                    string version = (string)type.GetProperty("Version").GetValue(info, null);
                    string name = (string)type.GetProperty("Name").GetValue(info, null);
                    bool clientside = Convert.ToInt32(type.GetProperty("NetworkMode").GetValue(info, null)) == 0;
                    var mod = new RuntimeMod(id, version, name, clientside);
                    if (mods.ContainsKey(mod.Id)) throw new InvalidOperationException("Duplicate SE Mod GUID: " + mod.Id);
                    mods.Add(mod.Id, mod);
                }
                if (!mods.ContainsKey(PluginId))
                    throw new InvalidOperationException("Script Extender asset registration is not ready.");
            }
            foreach (PluginInfo plugin in Chainloader.PluginInfos.Values)
            {
                if (ReferenceEquals(plugin.Instance, null)) continue;
                BepInPlugin meta = plugin.Metadata;
                if (!mods.ContainsKey(meta.GUID))
                    mods.Add(meta.GUID, new RuntimeMod(meta.GUID, meta.Version.ToString(), meta.Name, false));
            }
            return new List<RuntimeMod>(mods.Values);
        }
    }

    internal sealed class RuntimeMod
    {
        internal readonly string Id;
        internal readonly string Version;
        internal readonly string Name;
        internal readonly bool Clientside;

        internal RuntimeMod(string id, string version, string name, bool clientside)
        {
            if (String.IsNullOrWhiteSpace(id) || String.IsNullOrWhiteSpace(version) ||
                id.Length > 200 || version.Length > 80 ||
                id.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0 ||
                version.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0)
                throw new InvalidOperationException("Invalid runtime Mod identity or version.");
            Id = id.Trim().ToLowerInvariant();
            Version = version.Trim();
            Name = String.IsNullOrWhiteSpace(name) ? Id : name.Replace('\r', ' ').Replace('\n', ' ');
            // Infrastructure cannot be made optional by an asset manifest using its GUID.
            Clientside = clientside && Id != ScriptExtenderBridge.PluginId && Id != "uuimgui" &&
                         Id != SCDEMultiplayerCompatibilityPlugin.PluginGuid;
        }
    }

    internal static class RuntimeProfile
    {
        internal static List<ProfileMod> Merge(List<ProfileMod> manager, List<RuntimeMod> runtime)
        {
            var result = new List<ProfileMod>(manager);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (RuntimeMod mod in runtime)
            {
                if (!seen.Add(mod.Id)) throw new InvalidOperationException("Duplicate runtime Mod GUID.");
                if (!mod.Clientside) result.Add(new ProfileMod("se:" + mod.Id, mod.Version, mod.Name));
            }
            result.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            return result;
        }

        internal static string Fingerprint(List<ProfileMod> mods)
        {
            var sorted = new List<ProfileMod>(mods);
            sorted.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            var canonical = new StringBuilder();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProfileMod mod in sorted)
            {
                if (String.IsNullOrWhiteSpace(mod.Id) || String.IsNullOrWhiteSpace(mod.Version) ||
                    mod.Id.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0 ||
                    mod.Version.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0 || !ids.Add(mod.Id))
                    throw new FormatException("Invalid or duplicate Mod identity.");
                if (canonical.Length > 0) canonical.Append('\n');
                canonical.Append(mod.Id).Append('\0').Append(mod.Version);
            }
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString())))
                    .Replace("-", "").ToLowerInvariant();
        }

        internal static string Serialize(List<ProfileMod> mods, string fingerprint)
        {
            var text = new StringBuilder("SCDEMM3|" + fingerprint);
            foreach (ProfileMod mod in mods)
                text.Append('\n').Append(Encode(mod.Id)).Append('|').Append(Encode(mod.Version))
                    .Append('|').Append(Encode(mod.Name));
            return text.ToString();
        }

        private static string Encode(string text) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(text)); }
    }
}
