using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace JiuyeAyan.SCDEMultiplayerCompatibility
{
    // Reuse SE's ZIP reader in read-only mode. Never construct its editable MapArchive here.
    internal static class SeWorkshopMetadata
    {
        private const int MaxInfoBytes = 256 * 1024;
        private static ConstructorInfo open;
        private static MethodInfo getEntry;
        private static MethodInfo getInputStream;
        private static PropertyInfo entrySize;
        private static MethodInfo parseJson;
        private static object jsonOptions;
        private static PropertyInfo jsonRoot;
        private static MethodInfo jsonProperty;
        private static MethodInfo jsonInteger;

        internal static void Initialize(Assembly zipAssembly)
        {
            Type zip = zipAssembly.GetType("ICSharpCode.SharpZipLib.Zip.ZipFile", true);
            Type entry = zipAssembly.GetType("ICSharpCode.SharpZipLib.Zip.ZipEntry", true);
            open = zip.GetConstructor(new[] { typeof(Stream) });
            getEntry = zip.GetMethod("GetEntry", new[] { typeof(string) });
            getInputStream = zip.GetMethod("GetInputStream", new[] { entry });
            entrySize = entry.GetProperty("Size");
            if (open == null || getEntry == null || getInputStream == null || entrySize == null)
                throw new MissingMemberException("Unsupported SE ZIP reader.");
            // Use the JSON library SE already loads; Unity does not ship the desktop CLR serializer.
            Assembly json = null;
            foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
                if (loaded.GetName().Name == "System.Text.Json") { json = loaded; break; }
            if (json == null) json = Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(zipAssembly.Location), "System.Text.Json.dll"));
            Type document = json.GetType("System.Text.Json.JsonDocument", true);
            Type options = json.GetType("System.Text.Json.JsonDocumentOptions", true);
            Type element = json.GetType("System.Text.Json.JsonElement", true);
            parseJson = document.GetMethod("Parse", new[] { typeof(string), options });
            jsonOptions = Activator.CreateInstance(options);
            jsonRoot = document.GetProperty("RootElement");
            jsonProperty = element.GetMethod("GetProperty", new[] { typeof(string) });
            jsonInteger = element.GetMethod("GetInt32", Type.EmptyTypes);
        }

        internal static bool IsPluginMap(string file)
        {
            if (open == null) return false;
            try
            {
                if (!String.Equals(Path.GetExtension(file), ".map", StringComparison.OrdinalIgnoreCase)) return false;
                using (var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var archive = (IDisposable)open.Invoke(new object[] { source }))
                {
                    object entry = getEntry.Invoke(archive, new object[] { "info.json" });
                    if (entry == null) return false;
                    long size = (long)entrySize.GetValue(entry, null);
                    if (size <= 0 || size > MaxInfoBytes) return false;
                    using (var input = (Stream)getInputStream.Invoke(archive, new[] { entry }))
                    using (var bytes = new MemoryStream())
                    {
                        byte[] buffer = new byte[4096];
                        int count;
                        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (bytes.Length + count > MaxInfoBytes) return false;
                            bytes.Write(buffer, 0, count);
                        }
                        using (var document = (IDisposable)parseJson.Invoke(null, new[] { (object)Encoding.UTF8.GetString(bytes.ToArray()), jsonOptions }))
                        {
                            object root = jsonRoot.GetValue(document, null);
                            object manifest = jsonProperty.Invoke(root, new object[] { "Manifest" });
                            return (int)jsonInteger.Invoke(manifest, null) == 1;
                        }
                    }
                }
            }
            catch { return false; } // Let SE handle ordinary maps, unknown formats and errors normally.
        }

    }
}
