using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Backport one entry guard without recompiling or otherwise changing the upstream implementation.
internal static class EarlyManagedGuard
{
    static int Main(string[] args)
    {
        try { Patch(args); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
    }

    static void Patch(string[] args)
    {
        if (args.Length != 4 && args.Length != 5)
            throw new ArgumentException("Expected input, output, game Managed directory, shared runtime directory and optional release version.");
        string digest;
        using (var hash = SHA256.Create()) digest = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(args[0]))).Replace("-", "");
        bool update = args.Length == 5;
        if (!update && digest != "F671634B7A3C0480EFF8CD757D871431DAF3E5B445A67BEC5B502A01C6C79EB1")
            throw new InvalidOperationException("Unsupported upstream SE binary; review the patch before updating.");
        using (var resolver = new CandidateResolver(Path.GetDirectoryName(args[0]), args[2], args[3]))
        using (var assembly = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { AssemblyResolver = resolver }))
        {
            var module = assembly.MainModule;
            if (update)
            {
                var expected = new Version(args[4].Split('-', '+')[0]);
                var actual = assembly.Name.Version;
                if (assembly.Name.Name != "SHCDESE" || actual.Major != expected.Major || actual.Minor != expected.Minor ||
                    Math.Max(0, actual.Build) != Math.Max(0, expected.Build) || Math.Max(0, actual.Revision) != Math.Max(0, expected.Revision))
                    throw new InvalidOperationException("SE release and assembly versions do not match.");
                if ((module.Attributes & ModuleAttributes.StrongNameSigned) != 0)
                    throw new InvalidOperationException("Signed SE assemblies require a reviewed integration.");
            }
            ValidateRegistry(module);
            resolver.ValidateDependencies(assembly);
            var type = module.GetType("SHCDESE.API.Components.Archive.MapModManager");
            if (type == null) throw new InvalidOperationException("SE managed updater entry point has changed.");
            var methods = type.Methods.Where(m => m.Name == "TryUpdateModsFromRemote").ToArray();
            var method = methods.Length == 1 ? methods[0] : null;
            if (method == null || !method.HasBody || !method.IsPrivate || method.IsStatic || method.HasGenericParameters ||
                method.Parameters.Count != 0 || method.ReturnType.FullName != "System.Void" ||
                method.Body.Instructions.Count == 0 || method.Body.Instructions.Any(i => Equals(i.Operand, "SCDEModManagerLaunchId")))
                throw new InvalidOperationException("SE managed updater method is not supported.");
            if (method.Body.Instructions.Count(i => (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
                i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.FullName == "Platform_Workshop" &&
                ((MethodReference)i.Operand).Name == "GetListOfSubscribedItemsPaths") != 1)
                throw new InvalidOperationException("SE managed updater Workshop call structure changed.");
            var original = method.Body.Instructions[0];
            var originalCount = method.Body.Instructions.Count;
            var environment = new TypeReference("System", "Environment", module, module.TypeSystem.CoreLibrary);
            var get = new MethodReference("GetEnvironmentVariable", module.TypeSystem.String, environment);
            get.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
            var empty = new MethodReference("IsNullOrEmpty", module.TypeSystem.Boolean, module.TypeSystem.String);
            empty.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
            var il = method.Body.GetILProcessor();
            il.InsertBefore(original, il.Create(OpCodes.Ldstr, "SCDEModManagerLaunchId"));
            il.InsertBefore(original, il.Create(OpCodes.Call, get));
            il.InsertBefore(original, il.Create(OpCodes.Call, empty));
            il.InsertBefore(original, il.Create(OpCodes.Brtrue, original));
            il.InsertBefore(original, il.Create(OpCodes.Ret));
            assembly.Write(args[1]);
            using (var verify = AssemblyDefinition.ReadAssembly(args[1]))
            {
                var patched = verify.MainModule.GetType(type.FullName).Methods.Single(m => m.Name == method.Name);
                if (patched.Body.Instructions.Count != originalCount + 5 ||
                    (string)patched.Body.Instructions[0].Operand != "SCDEModManagerLaunchId" ||
                    patched.Body.Instructions[3].Operand != patched.Body.Instructions[5] ||
                    patched.Body.Instructions[4].OpCode != OpCodes.Ret)
                    throw new InvalidOperationException("Early SE guard verification failed.");
                var expected = method.Body.Instructions.Skip(5).Select(i => i.OpCode.ToString()).ToArray();
                var actual = patched.Body.Instructions.Skip(5).Select(i => i.OpCode.ToString()).ToArray();
                if (!expected.SequenceEqual(actual)) throw new InvalidOperationException("Original updater body changed.");
            }
            Console.WriteLine("SE_EARLY_MANAGED_GUARD_OK prefixInstructions=5 originalBodyPreserved=true registryContract=true sharedRuntimeReferences=true");
        }
    }

    static PropertyDefinition RequireProperty(TypeDefinition type, string name, bool isStatic)
    {
        var property = type == null ? null : type.Properties.SingleOrDefault(p => p.Name == name);
        var getter = property == null ? null : property.GetMethod;
        if (getter == null || !getter.IsPublic || getter.IsStatic != isStatic || property.Parameters.Count != 0)
            throw new InvalidOperationException("Unsupported SE registry property: " + name + ".");
        return property;
    }

    // These are the members read through reflection by MMC's ScriptExtenderBridge.
    static void ValidateRegistry(ModuleDefinition module)
    {
        var registry = module.GetType("SHCDESE.API.Components.ModManager.GameAssetModManager");
        if (registry == null || RequireProperty(registry, "Instance", true).PropertyType.FullName != registry.FullName)
            throw new InvalidOperationException("Unsupported SE registry singleton.");
        var method = registry.Methods.SingleOrDefault(m => m.Name == "GetRegisteredAssetDirectories");
        if (method == null || !method.IsPublic || method.IsStatic || method.HasGenericParameters || method.Parameters.Count != 0)
            throw new InvalidOperationException("Unsupported SE asset directory registry method.");
        var enumerable = method.ReturnType as GenericInstanceType;
        var entry = enumerable == null || enumerable.GenericArguments.Count != 1 ? null : enumerable.GenericArguments[0] as GenericInstanceType;
        if (enumerable == null || enumerable.ElementType.FullName != "System.Collections.Generic.IEnumerable`1" ||
            entry == null || entry.ElementType.FullName != "System.Collections.Generic.KeyValuePair`2" ||
            entry.GenericArguments.Count != 2 || entry.GenericArguments[1].FullName != "System.String")
            throw new InvalidOperationException("Unsupported SE asset directory registry return type.");
        var info = entry.GenericArguments[0].Resolve();
        foreach (string name in new[] { "GUID", "Version", "Name" })
            if (RequireProperty(info, name, false).PropertyType.FullName != "System.String")
                throw new InvalidOperationException("Unsupported SE Mod identity property: " + name + ".");
        var network = RequireProperty(info, "NetworkMode", false).PropertyType.Resolve();
        var clientside = network == null ? null : network.Fields.SingleOrDefault(f => f.Name == "Clientside");
        if (network == null || !network.IsEnum || clientside == null || !clientside.HasConstant || Convert.ToInt64(clientside.Constant) != 0)
            throw new InvalidOperationException("Unsupported SE Clientside network mode.");
    }

    // Never let a candidate-provided DLL or the helper's own Cecil satisfy a shared-runtime reference.
    sealed class CandidateResolver : IAssemblyResolver
    {
        readonly string candidateRoot, gameRoot, runtimeRoot;
        readonly Dictionary<string, AssemblyDefinition> loaded = new Dictionary<string, AssemblyDefinition>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> shared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal CandidateResolver(string candidate, string game, string runtime)
        {
            candidateRoot = Path.GetFullPath(candidate); gameRoot = Path.GetFullPath(game); runtimeRoot = Path.GetFullPath(runtime);
            foreach (string file in Directory.GetFiles(runtimeRoot, "*.dll")) shared.Add(Path.GetFileNameWithoutExtension(file));
            if (!shared.Contains("BepInEx")) throw new InvalidOperationException("The authoritative shared BepInEx runtime is missing.");
            foreach (string name in shared)
                if (File.Exists(Path.Combine(candidateRoot, name + ".dll")))
                    throw new InvalidOperationException("SE candidate shadows shared runtime assembly: " + name + ".");
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name) { return Resolve(name, new ReaderParameters()); }
        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            AssemblyDefinition assembly;
            if (!loaded.TryGetValue(name.Name, out assembly))
            {
                var roots = shared.Contains(name.Name) ? new[] { runtimeRoot } : new[] { candidateRoot, gameRoot };
                string file = roots.Select(root => Path.Combine(root, name.Name + ".dll")).FirstOrDefault(File.Exists);
                if (file == null) throw new InvalidOperationException("SE dependency is unavailable: " + name.FullName + ".");
                parameters.AssemblyResolver = this;
                assembly = AssemblyDefinition.ReadAssembly(file, parameters);
                loaded.Add(name.Name, assembly);
            }
            if (assembly.Name.Name != name.Name || !assembly.Name.PublicKeyToken.SequenceEqual(name.PublicKeyToken))
                throw new InvalidOperationException("SE dependency identity mismatch: " + name.FullName + ".");
            return assembly;
        }

        internal void ValidateDependencies(AssemblyDefinition candidate)
        {
            var pending = new Queue<AssemblyDefinition>(); pending.Enqueue(candidate);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (pending.Count != 0)
            {
                var current = pending.Dequeue();
                if (!visited.Add(current.Name.Name)) continue;
                foreach (var reference in current.MainModule.AssemblyReferences)
                {
                    // Unity strips unused framework assemblies. Check SE's own dependencies and
                    // shared bindings, not every optional code path inside third-party libraries.
                    if (current != candidate && !shared.Contains(reference.Name) &&
                        !File.Exists(Path.Combine(candidateRoot, reference.Name + ".dll"))) continue;
                    var dependency = Resolve(reference);
                    if (Path.GetDirectoryName(dependency.MainModule.FileName) == candidateRoot) pending.Enqueue(dependency);
                }
                foreach (var type in current.MainModule.GetTypeReferences())
                {
                    var scope = type.GetElementType().Scope as AssemblyNameReference;
                    if (scope != null && shared.Contains(scope.Name) && type.Resolve() == null)
                        throw new InvalidOperationException("SE shared runtime type is unavailable: " + type.FullName + ".");
                }
                foreach (var member in current.MainModule.GetMemberReferences())
                {
                    var scope = member.DeclaringType.GetElementType().Scope as AssemblyNameReference;
                    if (scope == null || !shared.Contains(scope.Name)) continue;
                    var method = member as MethodReference;
                    var field = member as FieldReference;
                    if ((method != null && (method.Resolve() == null || method.Resolve().IsStatic == method.HasThis)) ||
                        (field != null && field.Resolve() == null))
                        throw new InvalidOperationException("SE shared runtime member is unavailable: " + member.FullName + ".");
                }
            }
        }

        public void Dispose() { foreach (var assembly in loaded.Values) assembly.Dispose(); }
    }
}
