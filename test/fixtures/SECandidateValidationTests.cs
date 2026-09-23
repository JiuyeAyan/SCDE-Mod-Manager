using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TypeAttributes = Mono.Cecil.TypeAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using PropertyAttributes = Mono.Cecil.PropertyAttributes;

internal static class SECandidateValidationTests
{
    static int Main(string[] args)
    {
        string[] cases = { "valid", "missing-registry", "instance-not-static", "registry-static", "registry-return",
            "identity-type", "missing-property", "network-value", "updater-public", "updater-overload",
            "missing-dependency", "missing-runtime", "shadow-runtime", "missing-runtime-method", "missing-runtime-field",
            "missing-runtime-type", "dependency-runtime-method", "release-version", "updater-anchor-missing", "updater-anchor-duplicate" };
        string[] errors = { "", "registry singleton", "registry property: Instance", "registry method", "registry return type",
            "identity property: GUID", "registry property: Version", "Clientside network mode", "updater method", "updater method",
            "dependency is unavailable", "runtime is missing", "shadows shared runtime", "runtime member is unavailable",
            "runtime member is unavailable", "runtime type is unavailable", "runtime member is unavailable", "versions do not match",
            "Workshop call structure", "Workshop call structure" };
        foreach (string name in cases)
        {
            string root = Path.Combine(args[0], name);
            Directory.CreateDirectory(root);
            CreateFixture(root, name);
            string input = Path.Combine(root, "candidate", "SHCDESE.dll"), output = Path.Combine(root, "patched.dll");
            byte[] original = File.ReadAllBytes(input);
            string error = null;
            try
            {
                typeof(EarlyManagedGuard).GetMethod("Patch", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                    new object[] { new[] { input, output, System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
                        Path.Combine(root, "runtime"), name == "release-version" ? "2.9.0" : "2.8.0" } });
            }
            catch (TargetInvocationException caught) { error = caught.InnerException.Message; }
            if (!original.SequenceEqual(File.ReadAllBytes(input))) throw new Exception(name + ": candidate input changed");
            if (name == "valid")
            {
                if (error != null || !File.Exists(output)) throw new Exception(name + ": " + error);
                using (var patched = AssemblyDefinition.ReadAssembly(output))
                {
                    var instructions = patched.MainModule.GetType("SHCDESE.API.Components.Archive.MapModManager")
                        .Methods.Single().Body.Instructions;
                    if (instructions.Count != 8 || instructions[3].Operand != instructions[5])
                        throw new Exception("Guard prefix or original method changed.");
                }
            }
            else if (error == null || File.Exists(output) || error.IndexOf(errors[Array.IndexOf(cases, name)], StringComparison.OrdinalIgnoreCase) < 0)
                throw new Exception(name + ": wrong rejection or candidate was patched: " + error);
            Console.WriteLine("PASS " + name + (error == null ? "" : " -> " + error));
        }
        Console.WriteLine("SE_CANDIDATE_TESTS_OK cases=" + cases.Length);
        return 0;
    }

    static TypeDefinition AddType(ModuleDefinition module, string ns, string name)
    {
        var type = new TypeDefinition(ns, name, TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        module.Types.Add(type); return type;
    }

    static MethodDefinition AddMethod(TypeDefinition type, string name, TypeReference returns, MethodAttributes attributes)
    {
        var method = new MethodDefinition(name, attributes, returns);
        type.Methods.Add(method); method.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret)); return method;
    }

    static void AddProperty(TypeDefinition type, string name, TypeReference returns, bool isStatic)
    {
        var method = AddMethod(type, "get_" + name, returns, MethodAttributes.Public | MethodAttributes.SpecialName |
            (isStatic ? MethodAttributes.Static : 0));
        var property = new PropertyDefinition(name, PropertyAttributes.None, returns) { GetMethod = method };
        type.Properties.Add(property);
    }

    static void RuntimeCall(ModuleDefinition module, MethodDefinition caller, string testCase)
    {
        var reference = new AssemblyNameReference("BepInEx", new Version(5, 4, 21, 0));
        module.AssemblyReferences.Add(reference);
        var api = new TypeReference("BepInEx", testCase == "missing-runtime-type" ? "RemovedAPI" : "API", module, reference);
        var il = caller.Body.GetILProcessor();
        if (testCase == "missing-runtime-field")
        {
            il.InsertBefore(caller.Body.Instructions.Last(), il.Create(OpCodes.Ldsfld, new FieldReference("RemovedField", module.TypeSystem.Int32, api)));
            il.InsertBefore(caller.Body.Instructions.Last(), il.Create(OpCodes.Pop));
        }
        else
            il.InsertBefore(caller.Body.Instructions.Last(), il.Create(OpCodes.Call,
                new MethodReference(testCase.EndsWith("runtime-method") ? "RemovedMethod" : "AvailableMethod", module.TypeSystem.Void, api)));
    }

    static void CreateFixture(string root, string testCase)
    {
        string candidateRoot = Path.Combine(root, "candidate"), runtimeRoot = Path.Combine(root, "runtime");
        Directory.CreateDirectory(candidateRoot); Directory.CreateDirectory(runtimeRoot);
        if (testCase != "missing-runtime")
        {
            using (var runtime = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("BepInEx", new Version(5, 4, 23, 5)), "BepInEx", ModuleKind.Dll))
            {
                var api = AddType(runtime.MainModule, "BepInEx", "API");
                AddMethod(api, "AvailableMethod", runtime.MainModule.TypeSystem.Void, MethodAttributes.Public | MethodAttributes.Static);
                runtime.Write(Path.Combine(runtimeRoot, "BepInEx.dll"));
            }
        }
        if (testCase == "shadow-runtime") File.Copy(Path.Combine(runtimeRoot, "BepInEx.dll"), Path.Combine(candidateRoot, "BepInEx.dll"));
        using (var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("SHCDESE", new Version(2, 8, 0, 0)), "SHCDESE", ModuleKind.Dll))
        {
            var module = assembly.MainModule;
            string ns = "SHCDESE.API.Components.ModManager";
            var registry = AddType(module, ns, testCase == "missing-registry" ? "MovedRegistry" : "GameAssetModManager");
            AddProperty(registry, "Instance", registry, testCase != "instance-not-static");
            var info = AddType(module, ns, "ModInfo");
            foreach (string property in new[] { "GUID", "Version", "Name" })
                if (testCase != "missing-property" || property != "Version")
                    AddProperty(info, property, testCase == "identity-type" && property == "GUID" ? module.TypeSystem.Int32 : module.TypeSystem.String, false);
            var network = new TypeDefinition(ns, "ModNetworkMode", TypeAttributes.Public | TypeAttributes.Sealed,
                module.ImportReference(typeof(Enum)));
            module.Types.Add(network);
            network.Fields.Add(new FieldDefinition("value__", FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName, module.TypeSystem.Int32));
            network.Fields.Add(new FieldDefinition("Clientside", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal, network)
                { Constant = testCase == "network-value" ? 1 : 0 });
            AddProperty(info, "NetworkMode", network, false);
            var pair = new GenericInstanceType(module.ImportReference(typeof(System.Collections.Generic.KeyValuePair<,>)));
            pair.GenericArguments.Add(info); pair.GenericArguments.Add(module.TypeSystem.String);
            var enumerable = new GenericInstanceType(module.ImportReference(typeof(System.Collections.Generic.IEnumerable<>)));
            enumerable.GenericArguments.Add(pair);
            AddMethod(registry, "GetRegisteredAssetDirectories", testCase == "registry-return" ? module.TypeSystem.Object : (TypeReference)enumerable,
                MethodAttributes.Public | (testCase == "registry-static" ? MethodAttributes.Static : 0));
            var updater = AddType(module, "SHCDESE.API.Components.Archive", "MapModManager");
            var update = AddMethod(updater, "TryUpdateModsFromRemote", module.TypeSystem.Void, testCase == "updater-public" ? MethodAttributes.Public : MethodAttributes.Private);
            var workshop = AddMethod(AddType(module, "", "Platform_Workshop"), "GetListOfSubscribedItemsPaths", module.TypeSystem.Object,
                MethodAttributes.Public | MethodAttributes.Static);
            for (int count = 0; count < (testCase == "updater-anchor-missing" ? 0 : testCase == "updater-anchor-duplicate" ? 2 : 1); count++)
            {
                update.Body.GetILProcessor().InsertBefore(update.Body.Instructions.Last(), Instruction.Create(OpCodes.Call, workshop));
                update.Body.GetILProcessor().InsertBefore(update.Body.Instructions.Last(), Instruction.Create(OpCodes.Pop));
            }
            if (testCase == "updater-overload")
                AddMethod(updater, "TryUpdateModsFromRemote", module.TypeSystem.Void, MethodAttributes.Private).Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
            var caller = AddMethod(info, "TouchRuntime", module.TypeSystem.Void, MethodAttributes.Public | MethodAttributes.Static);
            RuntimeCall(module, caller, testCase == "dependency-runtime-method" ? "valid" : testCase);
            if (testCase == "missing-dependency") module.AssemblyReferences.Add(new AssemblyNameReference("AbsentDependency", new Version(1, 0)));
            if (testCase == "dependency-runtime-method")
            {
                using (var dependency = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("Library", new Version(1, 0)), "Library", ModuleKind.Dll))
                {
                    RuntimeCall(dependency.MainModule, AddMethod(AddType(dependency.MainModule, "Example", "Library"), "Call",
                        dependency.MainModule.TypeSystem.Void, MethodAttributes.Public | MethodAttributes.Static), testCase);
                    dependency.Write(Path.Combine(candidateRoot, "Library.dll"));
                }
                module.AssemblyReferences.Add(new AssemblyNameReference("Library", new Version(1, 0)));
            }
            assembly.Write(Path.Combine(candidateRoot, "SHCDESE.dll"));
        }
    }
}
