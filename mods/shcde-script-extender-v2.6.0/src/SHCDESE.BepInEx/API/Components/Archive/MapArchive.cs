using ICSharpCode.SharpZipLib.Zip;
using Serilog;
using SHCDESE.API.Components.ModManager;
using SHCDESE.IO;
using SHCDESE.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SHCDESE.API.Components.Archive;

/// <summary>
/// Represents a zip archive that is appended to a .map or .sav file.
/// This class handles finding, reading, and interacting with the embedded zip data.
/// It implements <see cref="IDisposable"/> to manage underlying file streams.
/// </summary>
public sealed class MapArchive : IDisposable
{
    /// <summary>
    /// Gets the underlying <see cref="ZipFile"/> instance for archive operations.
    /// </summary>
    public ZipFile? Archive { get; private set; }

    /// <summary>
    /// Gets the deserialized map metadata from 'info.json', if present.
    /// </summary>
    public ModInfo? Info { get; private set; }

    /// <summary>
    /// Gets the <see cref="MemoryStream"/> that holds the raw zip archive data.
    /// </summary>
    public MemoryStream? ArchiveStream { get; private set; }

    private const int MAX_SIZE = 1024 * 1024 * 32;

    private long _archiveOffset;

    private string _filePath;

    /// <summary>
    /// Gets a value indicating whether the map archive was successfully found and loaded.
    /// </summary>
    public bool IsValid = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="MapArchive"/> class by reading zip data from a given file path.
    /// </summary>
    /// <param name="filePath">The path to the map file to scan for an appended zip archive.</param>

    public MapArchive(string filePath)
    {
        this._filePath = filePath;

        // Validate the file contains a ZIP structure by finding the EOCD record.
        long eocdOffset = ZipUtil.FindEocdHeaderIndex(_filePath);
        if (eocdOffset == -1)
        {
            LogHelper.Warning($"ZIP EOCD-Header not found for [{_filePath}]. Not a valid archive.");
            IsValid = false;
            return;
        }

        // Find the actual start of the archive data
        _archiveOffset = ZipUtil.FindFirstZipHeaderIndex(_filePath);

        // Handle the empty ZIP case
        if (_archiveOffset == -1)
        {
            LogHelper.Information($"EOCD found but no file headers. Treating as an empty ZIP archive.");
            _archiveOffset = eocdOffset;
        }

        LogHelper.Information($"Reading ZIP data from offset: {_archiveOffset.ToString("X8")}");
        try
        {
            using (FileStream fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Seek(_archiveOffset, SeekOrigin.Begin);
                MemoryStream archiveStream = new MemoryStream(checked((int)(fs.Length - _archiveOffset)));
                ArchiveStream = archiveStream;
                fs.CopyTo(archiveStream);
                archiveStream.Position = 0;

                // Keep the original compressed entries intact. SharpZipLib only
                // decompresses an entry when it is actually read, and the expandable
                // MemoryStream still supports later BeginUpdate/CommitUpdate calls.
                Archive = new ZipFile(archiveStream, leaveOpen: true);
            }

            IsValid = true;

            // Try to read info.json if present
            TryReadInfo();
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Failed to process ZIP archive for [{_filePath}]. Exception: {ex.Message}");
            IsValid = false;
            Archive?.Close();
            ArchiveStream?.Dispose();
        }
    }

    private MapArchive()
    {

    }

    /// <summary>
    /// Creates a new empty MapArchive without attempting to load existing data.
    /// Used for when you need to create an archive for a file that doesn't have one yet.
    /// </summary>
    /// <param name="filePath">The path to the .map or .sav file.</param>
    /// <returns>A new empty MapArchive instance.</returns>
    public static MapArchive CreateEmpty(string filePath)
    {
        LogHelper.Information($"Creating empty archive for [{filePath}]");

        var archive = new MapArchive();
        archive._filePath = filePath;
        archive._archiveOffset = 0;

        // Create an empty, expandable MemoryStream
        archive.ArchiveStream = new MemoryStream();

        // Create a new, empty ZipFile
        archive.Archive = new ZipFile(archive.ArchiveStream, leaveOpen: true);
        archive.IsValid = true;

        LogHelper.Information($"Empty archive created successfully");

        return archive;
    }

    /// <summary>
    /// Attempts to load a map archive from the specified file path.
    /// </summary>
    /// <param name="filePath">The path of the file to load.</param>
    /// <param name="mapArchive">When this method returns, contains the loaded <see cref="MapArchive"/> if successful, or <c>null</c> otherwise.</param>
    /// <returns><c>true</c> if the archive was found and loaded successfully; otherwise, <c>false</c>.</returns>
    public static bool TryLoad(string filePath, [NotNullWhen(true)] out MapArchive? mapArchive)
    {
        mapArchive = null;

        if (!File.Exists(filePath))
        {
            LogHelper.Error($"File not found: {filePath}");
            return false;
        }
        mapArchive = new MapArchive(filePath);
        if (!mapArchive.IsValid)
        {
            mapArchive = null;
            return false;
        }

        if (mapArchive.Info == null)
        {
            LogHelper.Warning($"No info.json found: {filePath}");
        }

        if (!mapArchive.IsArchiveSafe())
        {
            LogHelper.Warning($"Archive has been deemed not safe: {filePath}");
            mapArchive.Dispose();
            mapArchive = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if the map archive has any unsafe files inside. (Crude)
    /// </summary>
    /// <returns>True if dangerous files found inside, false otherwise.</returns>
    private bool IsArchiveSafe()
    {
        if (Archive == null)
            return true;

        // If this is a BepInEx map, all bets are off.
        if (Info != null && Info.Manifest == ModManifest.BepInEx)
            return true;

        string[] nonSafeExtensions = { "exe", "dll", "dylib", "so", "bat", "sh", "ps1", "pwsh", "cmd" };
        foreach (ZipEntry? zipEntry in Archive)
        {
            if (zipEntry == null) continue;
            foreach (string ext in nonSafeExtensions)
            {
                if (zipEntry.Name.EndsWith(ext))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Attempts to read and deserialize 'info.json' from the archive root.
    /// </summary>
    /// <returns>True if 'info.json' was found and deserialized successfully, false otherwise.</returns>
    private bool TryReadInfo()
    {
        string? infoJson = TryReadTextFile("info.json", true);
        if (string.IsNullOrEmpty(infoJson))
            return false;

        Info = JsonSerializer.Deserialize<ModInfo>(infoJson);
        return true;
    }

    /// <summary>
    /// Generates a string listing all file and directory entries in the archive.
    /// </summary>
    /// <returns>A formatted string of archive contents.</returns>
    public string PrintAllEntries()
    {
        if (Archive == null)
        {
            LogHelper.Warning("Archive is null");
            return string.Empty;
        }

        StringBuilder sb = new StringBuilder();
        foreach (ZipEntry? entry in Archive)
        {
            if (entry == null)
                continue;

            if (entry.IsFile)
                sb.AppendLine($"FILE:\t{entry.Name}]");
            else if (entry.IsDirectory)
                sb.AppendLine($"DIR.:\t{entry.Name}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string containing the number of files in the archive.</returns>
    public override string ToString()
    {
        return $"ZIP: files=[{Archive?.Count}]";
    }

    /// <summary>
    /// Reads an entry from the archive as a UTF-8 encoded string.
    /// </summary>
    /// <param name="entryName">The name of the entry to read.</param>
    /// <param name="ignoreCase">If true, the search for the entry is case-insensitive.</param>
    /// <returns>The content of the file as a string, or an empty string if not found.</returns>
    public string TryReadTextFile(string entryName, bool ignoreCase = true)
    {
        int index = Archive!.FindEntry(entryName, ignoreCase);
        if (index == -1)
            return string.Empty;
        ZipEntry? entry = Archive[index];
        if ((entry == null) || !entry.IsFile)
            return string.Empty;

        using Stream stream = Archive.GetInputStream(entry);
        using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Reads an entry from the archive as a byte array.
    /// </summary>
    /// <param name="entryName">The name of the entry to read.</param>
    /// <param name="ignoreCase">If true, the search for the entry is case-insensitive.</param>
    /// <returns>The content of the file as a byte array, or <c>null</c> if not found.</returns>
    public byte[]? TryReadBinaryFile(string entryName, bool ignoreCase = true)
    {
        int index = Archive!.FindEntry(entryName, ignoreCase);
        if (index == -1)
            return null;
        ZipEntry? entry = Archive[index];
        if ((entry == null) || !entry.IsFile)
            return null;

        using Stream stream = Archive.GetInputStream(entry);
        using MemoryStream reader = new MemoryStream();
        stream.CopyTo(reader);
        return reader.ToArray();
    }

    /// <summary>
    /// Writes string content to a new or existing entry in the archive.
    /// </summary>
    /// <param name="entryName">The name of the entry to write to.</param>
    /// <param name="contents">The string content to write.</param>
    /// <param name="ignoreCase">If true, checks for existing entries in a case-insensitive manner.</param>
    /// <param name="overwrite">If true, any existing entry with the same name will be replaced.</param>
    /// <returns><c>true</c> if the write operation was successful; otherwise, <c>false</c>.</returns>
    public bool TryWriteTextFile(string entryName, string contents, bool ignoreCase = true, bool overwrite = true)
    {
        if (Archive == null)
        {
            LogHelper.Error($"Write (text) [{entryName}, {contents.Length} characters] - Failed due to Archive being null.");
            return false;
        }

        if (!Archive.IsUpdating)
            Archive!.BeginUpdate();

        int index = Archive.FindEntry(entryName, ignoreCase);
        if (index != -1 && !overwrite)
            return false;

        // If overwrite, remove existing entry first
        if (index != -1)
            Archive.Delete(entryName);

        byte[] data = Encoding.UTF8.GetBytes(contents);
        MemoryStream memoryStream = new MemoryStream(data);

        StreamDataSource dataSource = new StreamDataSource(memoryStream);
        Archive.Add(dataSource, entryName, CompressionMethod.Deflated);
        return true;
    }

    /// <summary>
    /// Writes byte content to a new or existing entry in the archive.
    /// </summary>
    /// <param name="entryName">The name of the entry to write to.</param>
    /// <param name="bytes">The byte content to write.</param>
    /// <param name="ignoreCase">If true, checks for existing entries in a case-insensitive manner.</param>
    /// <param name="overwrite">If true, any existing entry with the same name will be replaced.</param>
    /// <returns><c>true</c> if the write operation was successful; otherwise, <c>false</c>.</returns>
    public bool TryWriteBinaryFile(string entryName, byte[] bytes, bool ignoreCase = true, bool overwrite = true)
    {
        LogHelper.Debug($"Write (binary) [{entryName}, {bytes.Length} bytes]");
        if (Archive == null)
        {
            LogHelper.Error($"Write (binary) [{entryName}, {bytes.Length} bytes] - Failed due to Archive being null.");
            return false;
        }

        if (!Archive.IsUpdating)
        {
            Archive!.BeginUpdate();
        }

        int index = Archive.FindEntry(entryName, ignoreCase);
        if (index != -1 && !overwrite)
            return false;

        // If overwrite, remove existing entry first
        if (index != -1)
            Archive.Delete(entryName);

        MemoryStream memoryStream = new MemoryStream(bytes);

        StreamDataSource dataSource = new StreamDataSource(memoryStream);
        Archive.Add(dataSource, entryName, CompressionMethod.Deflated);

        return true;
    }

    /// <summary>
    /// Extracts a specific folder (and all its contents) from the archive to a destination on disk.
    /// </summary>
    /// <param name="sourceFolderInZip">The path inside the ZIP to extract (e.g., "Data/Textures"). Pass empty string or "/" to extract the entire archive.</param>
    /// <param name="destinationPath">The physical directory on disk where the files should be placed.</param>
    /// <param name="overwrite">If true, overwrites existing files at the destination.</param>
    /// <returns>True if the operation completed without critical errors; otherwise false.</returns>
    public bool TryExtractFolder(string sourceFolderInZip, string destinationPath, bool overwrite = true)
    {
        if (Archive == null)
        {
            LogHelper.Error("Attempted to extract folder, but Archive is null.");
            return false;
        }

        try
        {
            string rootPath = sourceFolderInZip.Replace('\\', '/').Trim('/');

            foreach (ZipEntry? entry in Archive)
            {
                if (entry == null)
                    continue;

                // Normalize entry name
                string entryName = entry.Name.Replace('\\', '/');

                // Determine if this entry belongs to the requested folder.
                // Its a match if:
                // rootPath is empty (extracting root).
                // The entry IS the rootPath (usually a directory entry).
                // The entry starts with "rootPath/".
                bool isMatch = string.IsNullOrEmpty(rootPath) ||
                               entryName.Equals(rootPath, StringComparison.InvariantCultureIgnoreCase) ||
                               entryName.StartsWith(rootPath + "/", StringComparison.InvariantCultureIgnoreCase);

                if (!isMatch)
                    continue;

                // Calculate the relative path for the destination.
                // If root is "Mods/A", and entry is "Mods/A/Textures/B.png", 
                // we want the destination relative path to be "Textures/B.png".
                string relativePath = string.IsNullOrEmpty(rootPath)
                    ? entryName
                    : entryName.Substring(rootPath.Length).TrimStart('/');

                if (string.IsNullOrEmpty(relativePath))
                {
                    // This happens if the entry IS the folder itself. 
                    // We ensure the base destination directory exists and continue.
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                // Combine destination + relative path
                string fullDestPath = Path.Combine(destinationPath, relativePath);

                // Security Check: Zip Slip Vulnerability Protection
                // Ensure the final path is actually inside the destination folder.
                string fullDestDir = Path.GetFullPath(destinationPath);
                string fullTarget = Path.GetFullPath(fullDestPath);
                if (!fullTarget.StartsWith(fullDestDir, StringComparison.InvariantCultureIgnoreCase))
                {
                    LogHelper.Warning($"Blocked unsafe extraction path (Zip Slip): {fullTarget}");
                    continue;
                }

                if (entry.IsDirectory)
                {
                    Directory.CreateDirectory(fullDestPath);
                }
                else
                {
                    // Ensure the directory for the file exists
                    string? dirName = Path.GetDirectoryName(fullDestPath);
                    if (!string.IsNullOrEmpty(dirName))
                    {
                        Directory.CreateDirectory(dirName);
                    }

                    // Check overwrite logic
                    if (File.Exists(fullDestPath) && !overwrite)
                    {
                        continue;
                    }

                    // Perform the extraction
                    using Stream zipStream = Archive.GetInputStream(entry);
                    using FileStream fs = File.Create(fullDestPath);
                    zipStream.CopyTo(fs);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Failed to extract folder [{sourceFolderInZip}] to [{destinationPath}].");
            return false;
        }
    }

    /// <summary>
    /// Apply all changes and write a working zip file in-memory.
    /// </summary>
    public void CommitChanges()
    {
        if (Archive != null && Archive.IsUpdating)
        {
            Archive.CommitUpdate();
        }
    }

    /// <summary>
    /// Provides a stream source for SharpZipLib from an existing stream.
    /// </summary>
    public class StreamDataSource : IStaticDataSource
    {
        private readonly Stream _stream;

        public StreamDataSource(Stream stream)
        {
            _stream = stream;
        }

        public Stream GetSource()
        {
            _stream.Position = 0;
            return _stream;
        }
    }

    /// <summary>
    /// Releases the resources used by the <see cref="MapArchive"/>, such as closing the archive and its underlying stream.
    /// </summary>
    public void Dispose()
    {
        Archive?.Close();
        ArchiveStream?.Dispose();
    }
}
