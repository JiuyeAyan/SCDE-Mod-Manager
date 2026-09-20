using System;
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
        string digest;
        using (var hash = SHA256.Create()) digest = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(args[0]))).Replace("-", "");
        bool update = args.Length == 5;
        if (!update && digest != "F671634B7A3C0480EFF8CD757D871431DAF3E5B445A67BEC5B502A01C6C79EB1")
            throw new InvalidOperationException("Unsupported upstream SE binary; review the patch before updating.");
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(args[0]));
        resolver.AddSearchDirectory(args[2]);
        resolver.AddSearchDirectory(args[3]);
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
            var type = module.GetType("SHCDESE.API.Components.Archive.MapModManager");
            if (type == null) throw new InvalidOperationException("SE managed updater entry point has changed.");
            var method = type.Methods.Single(m => m.Name == "TryUpdateModsFromRemote" && m.Parameters.Count == 0);
            if (!method.HasBody || method.IsStatic || method.ReturnType.FullName != "System.Void" ||
                method.Body.Instructions.Count == 0 || method.Body.Instructions.Any(i => Equals(i.Operand, "SCDEModManagerLaunchId")))
                throw new InvalidOperationException("SE managed updater method is not supported.");
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
            Console.WriteLine("SE_EARLY_MANAGED_GUARD_OK prefixInstructions=5 originalBodyPreserved=true");
        }
    }
}
