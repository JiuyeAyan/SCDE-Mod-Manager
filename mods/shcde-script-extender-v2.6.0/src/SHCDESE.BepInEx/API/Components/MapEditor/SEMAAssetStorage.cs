using SHCDESE.API.Components.Assets;
using SHCDESE.API.Components.ModManager;
using SHCDESE.IO;
using SHCDESE.Logging;
using System;
using System.IO;

namespace SHCDESE.API.Components.MapEditor;

/// <summary>
/// Resolves SEMA resources without exposing absolute paths to Lua. Loose-mod exports are
/// written to that mod's resource tree; packed-mod exports use a mod-scoped persistent overlay.
/// </summary>
internal static class SEMAAssetStorage
{
    private const string ExportDirectoryName = "SEMAExports";

    internal static bool TryWrite(string assetProviderGuid, string relativeAssetPath, byte[] content)
    {
        if (!TryNormalizeAddress(assetProviderGuid, relativeAssetPath, out string normalizedPath))
            return false;

        string exportPath;
        if (!TryResolveLooseModPath(assetProviderGuid, normalizedPath, out exportPath))
            exportPath = ResolvePersistentPath(assetProviderGuid, normalizedPath);

        string? directory = Path.GetDirectoryName(exportPath);
        if (directory == null)
            return false;

        Directory.CreateDirectory(directory);
        File.WriteAllBytes(exportPath, content);
        return true;
    }

    internal static bool TryRead(string assetProviderGuid, string relativeAssetPath, out byte[] content)
    {
        content = [];
        if (!TryNormalizeAddress(assetProviderGuid, relativeAssetPath, out string normalizedPath))
            return false;

        // Editor exports take precedence and remain immediately importable without
        // mutating or rebuilding the asset manager's read-only resource index.
        string persistentPath = ResolvePersistentPath(assetProviderGuid, normalizedPath);
        if (File.Exists(persistentPath))
        {
            content = File.ReadAllBytes(persistentPath);
            return true;
        }

        if (TryResolveLooseModPath(assetProviderGuid, normalizedPath, out string loosePath) && File.Exists(loosePath))
        {
            content = File.ReadAllBytes(loosePath);
            return true;
        }

        if (GameAssetManagerAPI.Instance.GetModFileBinaryContent(assetProviderGuid, normalizedPath, out byte[]? assetContent))
        {
            content = assetContent;
            return true;
        }

        LogHelper.Error($"SEMA asset [{normalizedPath}] was not found for provider [{assetProviderGuid}].");
        return false;
    }

    private static bool TryNormalizeAddress(string assetProviderGuid, string relativeAssetPath, out string normalizedPath)
    {
        normalizedPath = string.Empty;

        if (!IsValidProviderGuid(assetProviderGuid) || !GameAssetModManager.Instance.IsRegistered(assetProviderGuid))
        {
            LogHelper.Error($"SEMA asset provider [{assetProviderGuid}] is invalid or is not registered.");
            return false;
        }

        if (!ModResourcePath.TryNormalize(relativeAssetPath, out normalizedPath) ||
            !normalizedPath.EndsWith(".sema", StringComparison.OrdinalIgnoreCase))
        {
            LogHelper.Error($"Invalid SEMA asset path: [{relativeAssetPath}]. Use a mod-relative .sema path.");
            return false;
        }

        return true;
    }

    private static bool TryResolveLooseModPath(string assetProviderGuid, string normalizedPath, out string path)
    {
        path = string.Empty;
        if (!GameAssetModManager.Instance.TryGetRegisteredDirectory(assetProviderGuid, out string containerPath) ||
            !Directory.Exists(containerPath))
            return false;

        path = ResolveContainedPath(containerPath, normalizedPath);
        return true;
    }

    private static string ResolvePersistentPath(string assetProviderGuid, string normalizedPath)
    {
        string providerRoot = Path.Combine(
            DirectoryHelpers.GamePersistentDirectory,
            ExportDirectoryName,
            assetProviderGuid);
        return ResolveContainedPath(providerRoot, normalizedPath);
    }

    private static string ResolveContainedPath(string root, string normalizedPath)
    {
        string fullRoot = Path.GetFullPath(root);
        string rootWithSeparator = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(Path.Combine(
            fullRoot,
            normalizedPath.Replace('/', Path.DirectorySeparatorChar)));

        // ModResourcePath has already rejected traversal. Keep this invariant explicit in
        // case its normalization rules change later.
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Normalized SEMA path escaped its provider directory.");
        return candidate;
    }

    private static bool IsValidProviderGuid(string guid)
    {
        if (string.IsNullOrWhiteSpace(guid) || guid != guid.Trim() || guid is "." or "..")
            return false;

        return guid.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
            guid.IndexOf(Path.DirectorySeparatorChar) < 0 &&
            guid.IndexOf(Path.AltDirectorySeparatorChar) < 0;
    }
}
