using System;
using System.Collections.Generic;
using System.IO;

namespace SHCDESE.API.Components.Assets;

internal static class ModResourcePath
{
    internal static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    internal static bool TryNormalize(string path, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
            return false;

        string candidate = path.Replace('\\', '/').TrimStart('/');
        if (candidate.Length == 0)
            return false;

        string[] segments = candidate.Split('/');
        List<string> cleanSegments = new(segments.Length);
        foreach (string segment in segments)
        {
            if (segment.Length == 0 || segment == ".")
                continue;
            if (segment == ".." || segment.IndexOf('\0') >= 0 || segment.IndexOf(':') >= 0)
                return false;
            cleanSegments.Add(segment);
        }

        if (cleanSegments.Count == 0)
            return false;

        normalized = string.Join("/", cleanSegments);
        return true;
    }
}
