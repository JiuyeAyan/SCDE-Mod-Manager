using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Linq;
using Mono.Cecil;

internal static class SparseKeyMapLoadTests
{
    private static string[] searchRoots;

    private static int Main(string[] args)
    {
        try { return Run(args); }
        catch (Exception error)
        {
            for (Exception current = error; current != null; current = current.InnerException)
                Console.Error.WriteLine(current.GetType().FullName + ": " + current.Message);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        searchRoots = new[] { args[0], args[1], Path.GetDirectoryName(args[2]) };
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        Assembly game = Assembly.LoadFrom(Path.Combine(args[0], "Assembly-CSharp.dll"));
        Type keyType = game.GetType("KeyManager", true);
        object manager = FormatterServices.GetUninitializedObject(keyType);
        FieldInfo mapField = keyType.GetField("functionMap", BindingFlags.Instance | BindingFlags.NonPublic);
        int[,] map = new int[203, 2];
        for (int i = 0; i < 203; i++) { map[i, 0] = -1; map[i, 1] = -1; }
        mapField.SetValue(manager, map);
        MethodInfo load = keyType.GetMethod("LoadFromString");
        const string sparse = "||KEYS||\nStanceStand:\nStanceDefensive:\nStanceAggressive:\n||KEYS||\n";
        bool reproduced = false;
        try { load.Invoke(manager, new object[] { sparse }); }
        catch (TargetInvocationException error)
        {
            reproduced = error.InnerException is System.Collections.Generic.KeyNotFoundException;
            if (!reproduced) throw;
        }
        if (!reproduced) throw new Exception("The original sparse-map load failure was not reproduced.");

        PatchAndVerify(Path.Combine(args[0], "Assembly-CSharp.dll"), sparse);
        Console.WriteLine("SPARSE_NATIVE_KEYMAP_OK original_failure_reproduced=true patched_load=true");
        return 0;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void PatchAndVerify(string source, string sparse)
    {
        string fixture = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SparseKeyMapFixture.dll");
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(source));
        using (AssemblyDefinition definition = AssemblyDefinition.ReadAssembly(source,
            new ReaderParameters { AssemblyResolver = resolver }))
        {
            definition.Name.Name = "SparseKeyMapFixture";
            MethodDefinition method = definition.MainModule.Types.Single(t => t.Name == "KeyManager")
                .Methods.Single(m => m.Name == "LoadFromString");
            int replaced = 0;
            foreach (var instruction in method.Body.Instructions)
            {
                var member = instruction.Operand as MethodReference;
                if (member == null || member.Name != "get_Item" ||
                    member.DeclaringType.FullName != "System.Collections.Generic.Dictionary`2<System.Int32,System.Boolean>")
                    continue;
                var replacement = new MethodReference("ContainsKey", definition.MainModule.TypeSystem.Boolean, member.DeclaringType);
                replacement.HasThis = true;
                replacement.Parameters.Add(new ParameterDefinition(member.Parameters[0].ParameterType));
                instruction.Operand = replacement;
                replaced++;
            }
            if (replaced != 3) throw new Exception("Expected exactly three unsafe stance-key lookups.");
            definition.Write(fixture);
        }
        Type keyType = Assembly.LoadFrom(fixture).GetType("KeyManager", true);
        object manager = FormatterServices.GetUninitializedObject(keyType);
        int[,] map = new int[203, 2];
        for (int i = 0; i < 203; i++) { map[i, 0] = -1; map[i, 1] = -1; }
        keyType.GetField("functionMap", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, map);
        MethodInfo load = keyType.GetMethod("LoadFromString");
        load.Invoke(manager, new object[] { sparse });
        load.Invoke(manager, new object[] { sparse });
    }

    private static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name + ".dll";
        foreach (string root in searchRoots)
        {
            string candidate = Path.Combine(root, name);
            if (File.Exists(candidate)) return Assembly.LoadFrom(candidate);
        }
        return null;
    }
}
