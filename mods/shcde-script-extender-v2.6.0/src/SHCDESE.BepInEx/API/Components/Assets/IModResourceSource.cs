using System;
using System.Collections.Generic;
using System.IO;

namespace SHCDESE.API.Components.Assets;

/// <summary>
/// A read-only source of normalized, mod-relative files. Implementations may be backed by a loose directory or by a packed .semod archive.
/// </summary>
internal interface IModResourceSource : IDisposable
{
    string ContainerPath { get; }
    string DisplayName { get; }

    IEnumerable<string> EnumerateFiles();
    bool Contains(string relativePath);
    Stream OpenRead(string relativePath);
    bool TryGetPhysicalPath(string relativePath, out string physicalPath);
}
