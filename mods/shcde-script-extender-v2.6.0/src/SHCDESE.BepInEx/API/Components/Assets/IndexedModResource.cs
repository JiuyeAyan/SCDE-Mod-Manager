using System.IO;
using System.Text;

namespace SHCDESE.API.Components.Assets;

internal sealed class IndexedModResource
{
    internal IndexedModResource(string modGuid, string path, IModResourceSource source)
    {
        ModGuid = modGuid;
        Path = path;
        Source = source;
    }

    internal string ModGuid { get; }
    internal string Path { get; }
    internal IModResourceSource Source { get; }

    internal Stream OpenRead() => Source.OpenRead(Path);

    internal byte[] ReadAllBytes()
    {
        using Stream input = OpenRead();
        using MemoryStream output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    internal string ReadAllText()
    {
        using Stream input = OpenRead();
        using StreamReader reader = new StreamReader(input, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    internal bool TryGetPhysicalPath(out string physicalPath) => Source.TryGetPhysicalPath(Path, out physicalPath);
}
