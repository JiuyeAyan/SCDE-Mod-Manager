using ICSharpCode.SharpZipLib.Zip;
using SHCDESE.IO;
using SHCDESE.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Security.Cryptography;
using System.Text;

namespace SHCDESE.API.Components.Assets;

/// <summary>Read-only resource source backed by a standard ZIP-format .semod file.</summary>
internal sealed class SEmodResourceSource : IModResourceSource
{
    private const int MaxEntries = 100_000;
    private const long MaxEntrySize = 512L * 1024L * 1024L;
    private const long MaxTotalSize = 4L * 1024L * 1024L * 1024L;

    private readonly FileStream _stream;
    private readonly ZipFile _archive;
    private readonly Dictionary<string, ZipEntry> _entries = new(ModResourcePath.Comparer);
    private readonly string _cacheRoot;

    internal SEmodResourceSource(string archivePath)
    {
        ContainerPath = Path.GetFullPath(archivePath);
        _stream = new FileStream(ContainerPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _archive = new ZipFile(_stream) { IsStreamOwner = false };

        try
        {
            long totalSize = 0;
            foreach (ZipEntry? entry in _archive)
            {
                if (entry == null || !entry.IsFile)
                    continue;
                if (entry.IsCrypted)
                    throw new InvalidDataException($"Encrypted package entries are not supported: [{entry.Name}]");
                if (_entries.Count >= MaxEntries)
                    throw new InvalidDataException($"Package contains more than {MaxEntries} files.");
                if (entry.Size < 0 || entry.Size > MaxEntrySize)
                    throw new InvalidDataException($"Package entry [{entry.Name}] has an invalid size.");

                totalSize += entry.Size;
                if (totalSize > MaxTotalSize)
                    throw new InvalidDataException("Package uncompressed size exceeds the safety limit.");
                if (!ModResourcePath.TryNormalize(entry.Name, out string normalized))
                    throw new InvalidDataException($"Package contains an unsafe path: [{entry.Name}]");
                if (_entries.ContainsKey(normalized))
                    throw new InvalidDataException($"Package contains a duplicate case-insensitive path: [{normalized}]");

                _entries.Add(normalized, entry);
            }

            FileInfo info = new(ContainerPath);
            string identity = ContainerPath + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks;
            string cacheKey = XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(identity)).ToString();
            _cacheRoot = Path.Combine(DirectoryHelpers.GamePersistentDirectory, "AssetCache", cacheKey);
        }
        catch
        {
            _archive.Close();
            _stream.Dispose();
            throw;
        }
    }

    public string ContainerPath { get; }
    public string DisplayName => ContainerPath;

    public IEnumerable<string> EnumerateFiles() => _entries.Keys;

    public bool Contains(string relativePath) =>
        ModResourcePath.TryNormalize(relativePath, out string normalized) && _entries.ContainsKey(normalized);

    public Stream OpenRead(string relativePath)
    {
        if (!ModResourcePath.TryNormalize(relativePath, out string normalized) || !_entries.TryGetValue(normalized, out ZipEntry? entry))
            throw new FileNotFoundException("Package resource was not found.", relativePath);
        return _archive.GetInputStream(entry);
    }

    public bool TryGetPhysicalPath(string relativePath, out string physicalPath)
    {
        physicalPath = string.Empty;
        if (!ModResourcePath.TryNormalize(relativePath, out string normalized) || !_entries.TryGetValue(normalized, out ZipEntry? entry))
            return false;

        string cacheRootWithSeparator = Path.GetFullPath(_cacheRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(Path.Combine(_cacheRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(cacheRootWithSeparator, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!File.Exists(candidate) || new FileInfo(candidate).Length != entry.Size)
        {
            string? parent = Path.GetDirectoryName(candidate);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            string temporary = candidate + ".tmp";
            using (Stream input = _archive.GetInputStream(entry))
            using (FileStream output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                input.CopyTo(output);

            if (File.Exists(candidate))
                File.Delete(candidate);
            File.Move(temporary, candidate);
            LogHelper.Debug($"Materialized package resource [{normalized}] to the asset cache.");
        }

        physicalPath = candidate;
        return true;
    }

    public void Dispose()
    {
        _archive.Close();
        _stream.Dispose();
    }
}
