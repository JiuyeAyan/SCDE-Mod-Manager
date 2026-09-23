using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

internal static class RuntimeProfileTests
{
    private static Assembly plugin;
    private static string[] roots;
    private static int assertions;
    private static int updaterCalls;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void UpdaterProbe() { updaterCalls++; }
    private static void OverloadedProbe() { updaterCalls++; }
    private static void OverloadedProbe(int value) { updaterCalls += value; }
    private static Type TypeOf(string name) { return plugin.GetType("JiuyeAyan.SCDEMultiplayerCompatibility." + name, true); }
    private static object Call(string type, string method, params object[] args)
    {
        return TypeOf(type).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    }
    private static object Mod(string id, string version, string name, bool clientside)
    {
        return Activator.CreateInstance(TypeOf("RuntimeMod"), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { id, version, name, clientside }, null);
    }
    private static IList ListOf(string type, params object[] items)
    {
        var list = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(TypeOf(type)));
        foreach (object item in items) list.Add(item);
        return list;
    }
    private static object Merge(params object[] mods) { return Call("RuntimeProfile", "Merge", ListOf("ProfileMod"), ListOf("RuntimeMod", mods)); }
    private static string Hash(object list) { return (string)Call("RuntimeProfile", "Fingerprint", list); }
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); assertions++; }
    private static int Main(string[] args)
    {
        try
        {
            roots = new[] { args[0], args[1], Path.GetDirectoryName(args[2]) };
            AppDomain.CurrentDomain.AssemblyResolve += (sender, request) => {
                foreach (string root in roots) {
                    string file = Path.Combine(root, new AssemblyName(request.Name).Name + ".dll");
                    if (File.Exists(file)) return Assembly.LoadFrom(file);
                }
                return null;
            };
            plugin = Assembly.LoadFrom(args[2]);
            object game = Mod("asset.game", "1.0", "Gameplay", false);
            object ui = Mod("asset.ui", "1.0", "UI", true);
            object baseline = Merge(game);
            Check(Hash(baseline) == Hash(Merge(game, ui)), "Client-only absence must be allowed");
            Check(Hash(Merge(game, ui)) == Hash(Merge(Mod("asset.ui", "9.0", "Changed title", true), game)), "Client-only version and order");
            Check(Hash(baseline) != Hash(Merge()), "Missing gameplay asset must fail");
            Check(Hash(baseline) != Hash(Merge(Mod("asset.game", "2.0", "Gameplay", false))), "Gameplay version mismatch must fail");
            Check(Hash(baseline) == Hash(Merge(Mod("ASSET.GAME", "1.0", "Translated name", false))), "GUID case and display name do not change identity");
            Check(Hash(baseline) == Hash(Merge(Mod(" asset.game ", "1.0.0.0", "Gameplay", false))), "Numeric version spellings normalize across package systems");
            Check(Hash(baseline) != Hash(Merge(Mod("asset.other", "1.0", "Gameplay", false))), "Runtime GUID mismatch must fail");
            object wrapper = Activator.CreateInstance(TypeOf("ProfileMod"), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { "manager-wrapper", "99.0", "Package receipt" }, null);
            Check(Hash(baseline) == Hash(Call("RuntimeProfile", "Merge", ListOf("ProfileMod", wrapper), ListOf("RuntimeMod", game))),
                "Same loaded runtime must match standalone MMC regardless of manager wrapper");
            Check(Hash(baseline) != Hash(Merge(Mod("asset.game", "1.0", "Gameplay", true))), "Conflicting network-mode declarations must fail");
            foreach (string core in new[] { "000shcdese", "uuimgui", "com.jiuyeayan.scde.multiplayer-compatibility" })
                Check(((IList)Merge(Mod(core, "1", core, true))).Count == 1, "Infrastructure cannot be client-only");
            bool duplicate = false;
            try { Merge(game, game); } catch (TargetInvocationException) { duplicate = true; }
            Check(duplicate, "Duplicate GUID must fail");
            var combined = (IDictionary)Activator.CreateInstance(typeof(System.Collections.Generic.Dictionary<,>).MakeGenericType(typeof(string), TypeOf("RuntimeMod")));
            combined.Add("asset.game", game);
            var assetIds = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Call("ScriptExtenderBridge", "AddAsset", combined, assetIds, Mod("ASSET.GAME", "1.0.0", "Optional metadata", true));
            Check(Object.ReferenceEquals(combined["asset.game"], game), "SE metadata cannot downgrade a loaded plugin to client-only");
            bool duplicateAsset = false;
            try { Call("ScriptExtenderBridge", "AddAsset", combined, assetIds, game); } catch (TargetInvocationException) { duplicateAsset = true; }
            Check(duplicateAsset, "Duplicate registry GUIDs must fail even when a plugin owns the identity");
            bool assetVersionMismatch = false;
            try { Call("ScriptExtenderBridge", "AddAsset", combined, new System.Collections.Generic.HashSet<string>(), Mod("asset.game", "2.0", "Stale metadata", false)); }
            catch (TargetInvocationException) { assetVersionMismatch = true; }
            Check(assetVersionMismatch, "SE metadata cannot hide a different loaded plugin version");
            string document = (string)Call("RuntimeProfile", "Serialize", baseline, Hash(baseline));
            Check(document.StartsWith("SCDEMM4|"), "New runtime schema must be separate from v3 and manager's disk receipt");
            var parser = TypeOf("SCDEMultiplayerCompatibilityPlugin").GetMethod("TryParseProfile", BindingFlags.NonPublic | BindingFlags.Static);
            Check((bool)parser.Invoke(null, new object[] { document, null }), "Combined profile round trip");
            string receipt = document.Replace("SCDEMM4|", "SCDEMM2|");
            Check((bool)parser.Invoke(null, new object[] { receipt, null }), "Manager disk profile remains readable");
            Check(!(bool)parser.Invoke(null, new object[] { document.Replace("SCDEMM4|", "SCDEMM3|"), null }), "Old network protocol must not be treated as verified");
            Check(!(bool)parser.Invoke(null, new object[] { document.Replace("MS4wLjAuMA==", "Mi4wLjAuMA=="), null }), "Tampered rows must not trust header token");
            Check(!(bool)parser.Invoke(null, new object[] { document + document.Substring(document.IndexOf('\n')), null }), "Duplicate profile rows must fail");
            Check(!(bool)parser.Invoke(null, new object[] { new string('x', 48001), null }), "Oversized profile must fail");
            string localToken = "4:" + Hash(baseline);
            foreach (object[] ownership in new[] {
                new object[] { "76561198000000001", 76561198000000001UL, true },
                new object[] { "76561198000000001", 76561198000000002UL, false },
                new object[] { "76561198000000001", 0UL, false },
                new object[] { "", 76561198000000001UL, false },
                new object[] { "not-an-owner", 76561198000000001UL, false },
                new object[] { "0", 0UL, false }
            })
                Check((bool)Call("SCDEMultiplayerCompatibilityPlugin", "HostOwnerMatches", ownership[0], ownership[1]) == (bool)ownership[2],
                    "Host profile requires an exact nonzero current Steam owner; inherited or unknown ownership rejects");
            foreach (object[] peer in new[] {
                new object[] { localToken, localToken, true, true, 1 },
                new object[] { localToken, localToken, true, false, 1 },
                new object[] { localToken, "4:" + Hash(Merge()), true, true, 0 },
                new object[] { localToken, "broken", true, true, 0 },
                new object[] { localToken, "3:" + Hash(baseline), true, true, 0 },
                new object[] { localToken, "", true, true, 0 },
                new object[] { localToken, "", true, false, 0 },
                new object[] { localToken, "", false, true, 2 },
                new object[] { localToken, "", false, false, 0 },
                new object[] { "", "", false, true, 0 },
                new object[] { "4:" + new string('x', 64), "4:" + new string('x', 64), true, true, 0 }
            })
                Check((int)Call("SCDEMultiplayerCompatibilityPlugin", "ClassifyPeer", peer[0], peer[1], peer[2], peer[3]) == (int)peer[4],
                    "SE-only interoperability is distinct from v4 verification; present/old/invalid MMC never downgrades");
            string metadata = File.ReadAllText(Path.Combine(Path.GetDirectoryName(args[2]), "info.json"));
            Check(metadata.Contains("\"NetworkMode\": 0"), "Upstream SE must treat MMC as optional client-side infrastructure for SE-only peers");
            Check(((IList)Merge(Mod("com.jiuyeayan.scde.multiplayer-compatibility", "0.4.0", "MMC", true))).Count == 1,
                "MMC remains required in its own strict v4 runtime profile despite SE-facing optional metadata");
            object hostMemory = Activator.CreateInstance(TypeOf("HostCapabilityMemory"), true);
            MethodInfo observeHost = TypeOf("HostCapabilityMemory").GetMethod("Observe", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (object[] observed in new[] {
                new object[] { 10UL, 100UL, true, 0UL, 10UL, true },
                new object[] { 10UL, 100UL, false, 0UL, 10UL, true },
                new object[] { 10UL, 100UL, false, 10UL, 0UL, true },
                new object[] { 10UL, 0UL, false, 10UL, 0UL, true },
                new object[] { 10UL, 101UL, false, 10UL, 0UL, false },
                new object[] { 10UL, 101UL, true, 10UL, 0UL, true },
                new object[] { 20UL, 200UL, false, 10UL, 20UL, false },
                new object[] { 10UL, 101UL, false, 10UL, 20UL, true },
                new object[] { 30UL, 300UL, false, 30UL, 0UL, false },
                new object[] { 10UL, 101UL, false, 0UL, 10UL, false }
            })
            {
                bool seen = (bool)observeHost.Invoke(hostMemory, new object[] { observed[0], observed[1], observed[2], observed[3], observed[4] });
                Check(seen == (bool)observed[5], "Host MMC evidence survives pending-to-joined/metadata withdrawal and resets only with owner/context change");
                if (seen)
                    Check((int)Call("SCDEMultiplayerCompatibilityPlugin", "ClassifyPeer", localToken, "", seen, true) == 0,
                        "Previously advertised MMC host cannot downgrade to SE-only fallback");
            }
            foreach (object[] test in new[] {
                new object[] { null, false, 0 }, new object[] { null, true, -1 },
                new object[] { "", false, -1 }, new object[] { "", true, -1 },
                new object[] { "broken", false, -1 }, new object[] { "broken", true, -1 },
                new object[] { receipt, false, 0 }, new object[] { receipt, true, 1 },
                new object[] { document, true, -1 }
            })
                Check((int)Call("SCDEMultiplayerCompatibilityPlugin", "ClassifyManagerProfile", test[0], test[1], null) == (int)test[2],
                    "Absent, valid and malformed receipt policies must differ; a network profile is not a receipt");
            string receiptRoot = Path.Combine(Path.GetTempPath(), "scdemm-receipt-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(receiptRoot);
            try
            {
                string receiptPath = Path.Combine(receiptRoot, "active-mods.lobby");
                Check(Call("SCDEMultiplayerCompatibilityPlugin", "ReadManagerReceipt", receiptPath) == null, "Only a missing receipt becomes absence");
                File.WriteAllText(receiptPath, receipt);
                Check((string)Call("SCDEMultiplayerCompatibilityPlugin", "ReadManagerReceipt", receiptPath) == receipt, "Bounded reader preserves a valid receipt");
                using (FileStream locked = File.Open(receiptPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    bool unreadableRejected = false;
                    try { Call("SCDEMultiplayerCompatibilityPlugin", "ReadManagerReceipt", receiptPath); } catch (TargetInvocationException) { unreadableRejected = true; }
                    Check(unreadableRejected, "Unreadable existing receipts never become standalone");
                }
                File.WriteAllText(receiptPath, new string('x', 48001));
                bool oversizeRejected = false;
                try { Call("SCDEMultiplayerCompatibilityPlugin", "ReadManagerReceipt", receiptPath); } catch (TargetInvocationException) { oversizeRejected = true; }
                Check(oversizeRejected, "Receipt reads are bounded before parsing");
            }
            finally { Directory.Delete(receiptRoot, true); }
            Environment.SetEnvironmentVariable("SCDEModManagerLaunchId", null);
            Call("StartupReadyReporter", "ReportFailure", "Rejected test target");
            Check((bool)TypeOf("StartupReadyReporter").GetField("finished", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null),
                "Initialization failure permanently suppresses startup ready");
            Call("PatchTargetGuard", "ValidateGameTargets");
            Check(true, "Current local game patch signatures and IL anchors validate without patching it");
            Call("PatchTargetGuard", "Require", typeof(RuntimeProfileTests), "UpdaterProbe", true, typeof(void), Type.EmptyTypes);
            foreach (object[] rejected in new[] {
                new object[] { typeof(RuntimeProfileTests), "UpdaterProbe", false, typeof(void), Type.EmptyTypes },
                new object[] { typeof(RuntimeProfileTests), "UpdaterProbe", true, typeof(int), Type.EmptyTypes },
                new object[] { typeof(RuntimeProfileTests), "UpdaterProbe", true, typeof(void), new[] { typeof(int) } },
                new object[] { typeof(RuntimeProfileTests), "OverloadedProbe", true, typeof(void), Type.EmptyTypes }
            })
            {
                bool rejectedTarget = false;
                try { Call("PatchTargetGuard", "Require", rejected); } catch (TargetInvocationException) { rejectedTarget = true; }
                Check(rejectedTarget, "Wrong signature or ambiguous patch targets must reject before patching");
            }
            bool changedBody = false;
            try { Call("PatchTargetGuard", "RequireCall", typeof(RuntimeProfileTests).GetMethod("UpdaterProbe", BindingFlags.NonPublic | BindingFlags.Static),
                "Platform_Workshop", "GetListOfSubscribedItemsPaths", 1); } catch (TargetInvocationException) { changedBody = true; }
            Check(changedBody, "Changed patch IL anchors must reject before patching");
            Assembly harmonyAssembly = Assembly.LoadFrom(Path.Combine(args[1], "0Harmony.dll"));
            Type instructionType = harmonyAssembly.GetType("HarmonyLib.CodeInstruction", true);
            var keyInstructions = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(instructionType));
            MethodInfo lookup = typeof(System.Collections.Generic.Dictionary<int, bool>).GetProperty("Item").GetGetMethod();
            foreach (int key in new[] { 114, 116, 121 })
            {
                keyInstructions.Add(Activator.CreateInstance(instructionType, new object[] { OpCodes.Ldc_I4, key }));
                keyInstructions.Add(Activator.CreateInstance(instructionType, new object[] { OpCodes.Callvirt, lookup }));
                keyInstructions.Add(Activator.CreateInstance(instructionType, new object[] { OpCodes.Brtrue, null }));
            }
            Call("PatchTargetGuard", "ValidateSparseKeyMap", keyInstructions);
            int replacements = 0;
            foreach (object instruction in (IEnumerable)Call("SparseNativeKeyMapLoadPatch", "Transpiler", keyInstructions))
            {
                MethodInfo operand = instructionType.GetField("operand").GetValue(instruction) as MethodInfo;
                if (operand != null && operand.Name == "ContainsKey") replacements++;
            }
            Check(replacements == 3, "Actual transpiler replaces exactly the three validated stance lookups");
            bool repeatRejected = false;
            try { Call("PatchTargetGuard", "ValidateSparseKeyMap", keyInstructions); } catch (TargetInvocationException) { repeatRejected = true; }
            Check(repeatRejected, "Already-rewritten or changed lookup count is not patched blindly");
            for (int index = 1; index < keyInstructions.Count; index += 3)
                instructionType.GetField("operand").SetValue(keyInstructions[index], lookup);
            instructionType.GetField("opcode").SetValue(keyInstructions[2], OpCodes.Brfalse);
            bool branchRejected = false;
            try { Call("PatchTargetGuard", "ValidateSparseKeyMap", keyInstructions); } catch (TargetInvocationException) { branchRejected = true; }
            Check(branchRejected, "Changed stance branch shape is rejected before any replacement");
            Type harmonyType = harmonyAssembly.GetType("HarmonyLib.Harmony", true);
            Type harmonyMethodType = harmonyAssembly.GetType("HarmonyLib.HarmonyMethod", true);
            object harmony = Activator.CreateInstance(harmonyType, new object[] { "scdemm.policy.test" });
            MethodInfo patch = null;
            foreach (MethodInfo candidate in harmonyType.GetMethods())
                if (candidate.Name == "Patch" && candidate.GetParameters().Length == 6)
                    patch = candidate;
            if (patch == null) throw new MissingMethodException("Expected Harmony.Patch overload was not found");
            object[] patchArgs = new object[patch.GetParameters().Length];
            patchArgs[0] = typeof(RuntimeProfileTests).GetMethod("UpdaterProbe", BindingFlags.NonPublic | BindingFlags.Static);
            patchArgs[1] = Activator.CreateInstance(harmonyMethodType, new object[] {
                TypeOf("ScriptExtenderBridge").GetMethod("BlockAutomaticDeployment", BindingFlags.NonPublic | BindingFlags.Static) });
            try {
                patch.Invoke(harmony, patchArgs);
                UpdaterProbe();
                Check(updaterCalls == 0, "Managed policy prefix must stop automatic deployment");
            } finally { harmonyType.GetMethod("UnpatchSelf").Invoke(harmony, null); }
            UpdaterProbe();
            Check(updaterCalls == 1, "Policy must be reversible and not affect unpatched standalone SE");
            Console.WriteLine("SE_RUNTIME_PROFILE_OK assertions=" + assertions);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
