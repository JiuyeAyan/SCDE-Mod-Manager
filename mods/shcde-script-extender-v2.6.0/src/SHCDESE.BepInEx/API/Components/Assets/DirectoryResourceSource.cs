using System;
using System.Collections.Generic;
using System.IO;

namespace SHCDESE.API.Components.Assets;

internal sealed class DirectoryResourceSource : IModResourceSource
{
    private readonly string _rootWithSeparator;

    internal DirectoryResourceSource(string directory)
    {
        ContainerPath = Path.GetFullPath(directory);
        _rootWithSeparator = ContainerPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
    }

    public string ContainerPath { get; }
    public string DisplayName => ContainerPath;

    public IEnumerable<string> EnumerateFiles()
    {
        foreach (string file in Directory.EnumerateFiles(ContainerPath, "*", SearchOption.AllDirectories))
        {
            // ContainerPath is absolute, so Directory.EnumerateFiles returns absolute paths.
            if (!file.StartsWith(_rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                continue;

            string relativePath = file.Substring(_rootWithSeparator.Length);
            if (ModResourcePath.TryNormalize(relativePath, out string normalized))
                yield return normalized;
        }
    }

    public bool Contains(string relativePath) => TryGetPhysicalPath(relativePath, out _);

    public Stream OpenRead(string relativePath)
    {
        if (!TryGetPhysicalPath(relativePath, out string physicalPath))
            throw new FileNotFoundException("Mod resource was not found.", relativePath);
        return new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public bool TryGetPhysicalPath(string relativePath, out string physicalPath)
    {
        physicalPath = string.Empty;
        if (!ModResourcePath.TryNormalize(relativePath, out string normalized))
            return false;

        string candidate = Path.GetFullPath(Path.Combine(ContainerPath, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(_rootWithSeparator, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate))
            return false;

        physicalPath = candidate;
        return true;
    }

    public void Dispose()
    {

    }
}
