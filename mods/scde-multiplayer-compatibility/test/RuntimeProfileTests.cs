using System;
using System.Collections;
using System.IO;
using System.Reflection;

internal static class RuntimeProfileTests
{
    private static Assembly plugin;
    private static string[] roots;
    private static int assertions;
    private static int updaterCalls;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void UpdaterProbe() { updaterCalls++; }
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
            Check(Hash(baseline) != Hash(Merge(Mod("asset.game", "1.0", "Gameplay", true))), "Conflicting network-mode declarations must fail");
            foreach (string core in new[] { "000shcdese", "uuimgui", "com.jiuyeayan.scde.multiplayer-compatibility" })
                Check(((IList)Merge(Mod(core, "1", core, true))).Count == 1, "Infrastructure cannot be client-only");
            bool duplicate = false;
            try { Merge(game, game); } catch (TargetInvocationException) { duplicate = true; }
            Check(duplicate, "Duplicate GUID must fail");
            string document = (string)Call("RuntimeProfile", "Serialize", baseline, Hash(baseline));
            Check(document.StartsWith("SCDEMM3|"), "Runtime schema must be separate from manager's disk receipt");
            var parser = TypeOf("SCDEMultiplayerCompatibilityPlugin").GetMethod("TryParseProfile", BindingFlags.NonPublic | BindingFlags.Static);
            Check((bool)parser.Invoke(null, new object[] { document, null }), "Combined profile round trip");
            Check((bool)parser.Invoke(null, new object[] { document.Replace("SCDEMM3|", "SCDEMM2|"), null }), "Manager disk profile remains readable");
            Check(!(bool)parser.Invoke(null, new object[] { document.Replace("MS4w", "Mi4w"), null }), "Tampered rows must not trust header token");
            Check(!(bool)parser.Invoke(null, new object[] { document + document.Substring(document.IndexOf('\n')), null }), "Duplicate profile rows must fail");
            Check(!(bool)parser.Invoke(null, new object[] { new string('x', 48001), null }), "Oversized profile must fail");
            Assembly harmonyAssembly = Assembly.LoadFrom(Path.Combine(args[1], "0Harmony.dll"));
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
