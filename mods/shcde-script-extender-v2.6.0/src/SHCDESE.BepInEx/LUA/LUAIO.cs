using SHCDESE.API;
using SHCDESE.API.Components.Archive;
using SHCDESE.API.Components.ModManager;
using SHCDESE.Extensions;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Collections.Generic;
using System.IO;

namespace SHCDESE.Lua;

/// <summary>
/// Provides sandboxed I/O functions for the Lua environment.
/// This class acts as a secure bridge, allowing Lua scripts to read files from approved sources without gaining access to the user's file system.
/// </summary>
/// <remarks>
/// All functions in this class are designed with security in mind (to some extent) to prevent malicious scripts from accessing system resources.
/// </remarks>
[LuaApiNamespace("IO")]
public static class LuaIO
{
    private static LuaExecutionContext _currentContext = LuaExecutionContext.MapArchive;
    private static string? _contextRootDirectory = null;
    private static string? _contextModGuid = null;

    /// <summary>
    /// Sets the current execution context for Lua I/O operations.
    /// This determines which file sources are accessible to Lua scripts.
    /// </summary>
    /// <param name="context">The execution context to set.</param>
    /// <param name="rootDirectory">
    /// For LordAI and AssetMod contexts, the absolute path to the root directory.
    /// This directory becomes the root for all file operations. Optional for MapArchive context.
    /// </param>
    internal static void SetExecutionContext(LuaExecutionContext context, string? rootDirectory = null, string? modGuid = null)
    {
        _currentContext = context;
        _contextRootDirectory = rootDirectory;
        _contextModGuid = modGuid;

        LogHelper.Information($"Lua execution context set to: {context}" +
            (rootDirectory != null ? $" with root: {rootDirectory}" : ""));
    }

    /// <summary>
    /// Resets the execution context to the default (MapArchive with no root directory).
    /// </summary>
    internal static void ResetExecutionContext()
    {
        _currentContext = LuaExecutionContext.MapArchive;
        _contextRootDirectory = null;
        _contextModGuid = null;
    }

    /// <summary>
    /// Registers the sandboxed I/O functions into the provided Lua state.
    /// </summary>
    /// <param name="lua">The <see cref="NLua.Lua"/> instance that the functions will be registered into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedStaticMethods(typeof(LuaIO));
    }

    /// <summary>
    /// Safely reads the text content of a file based on the current execution context.
    /// </summary>
    /// <param name="entryName">
    /// The relative path of the file to read.
    /// For MapArchive: path within the zip archive (e.g., "scripts/my_module.lua").
    /// For LordAI/AssetMod: path relative to the context root directory.
    /// </param>
    /// <returns>
    /// The text content of the file if found and the path is valid; otherwise, returns <see cref="string.Empty"/>.
    /// </returns>
    [LuaApiExport("GetFileText")]
    public static string GetArchiveFileText(string entryName)
    {
        // Sanitize the path to prevent directory traversal attacks
        if (!IsPathSafe(entryName))
        {
            LogHelper.Warning($"Unsafe path rejected: {entryName}");
            return string.Empty;
        }

        return _currentContext switch
        {
            LuaExecutionContext.MapArchive => GetFileFromMapArchive(entryName),
            LuaExecutionContext.LordAI => GetFileFromContextDirectory(entryName),
            LuaExecutionContext.AssetMod => GetFileFromAssetModSources(entryName),
            _ => string.Empty
        };
    }

    /// <summary>Reads a file from a specific mod's indexed private namespace.</summary>
    [LuaApiExport("GetModFileText")]
    public static string GetModFileText(string modGuid, string entryName)
    {
        if (string.IsNullOrWhiteSpace(modGuid) || !IsPathSafe(entryName))
            return string.Empty;
        return GameAssetManagerAPI.Instance.GetModFileTextContent(modGuid, entryName, out string content) ? content : string.Empty;
    }

    /// <summary>
    /// Lists all available Lua module files (.lua) in the current context.
    /// Useful for debugging and discovery.
    /// </summary>
    /// <param name="directory">Optional subdirectory to search within (e.g., "scripts" or "lib").</param>
    /// <returns>Array of available module paths (without .lua extension, using dot notation).</returns>
    [LuaApiExport("ListModules")]
    public static string[] ListAvailableModules(string directory = "")
    {
        // Sanitize directory path
        if (!string.IsNullOrEmpty(directory) && !IsPathSafe(directory))
        {
            LogHelper.Warning($"Unsafe directory path rejected: {directory}");
            return new string[0];
        }

        List<string> modules = new List<string>();

        switch (_currentContext)
        {
            case LuaExecutionContext.MapArchive:
                modules.AddRange(ListModulesInMapArchive(directory));
                break;

            case LuaExecutionContext.LordAI:
                if (_contextRootDirectory != null)
                {
                    modules.AddRange(ListModulesInDirectory(_contextRootDirectory, directory));
                }
                break;
            case LuaExecutionContext.AssetMod:
                if (_contextModGuid != null)
                    modules.AddRange(ListModulesInAssetMod(_contextModGuid, directory));
                break;
        }

        return modules.ToArray();
    }

    // ============================================================================
    // Private Helper Methods
    // ============================================================================

    /// <summary>
    /// Validates that a path is safe and doesn't contain directory traversal attempts.
    /// </summary>
    private static bool IsPathSafe(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // Reject paths with directory traversal attempts
        if (path.Contains(".."))
            return false;

        // Reject absolute paths (rooted paths)
        if (Path.IsPathRooted(path))
            return false;

        // Reject paths that start with separators
        if (path.StartsWith("/") || path.StartsWith("\\"))
            return false;

        return true;
    }

    /// <summary>
    /// Reads a file from the map archive.
    /// </summary>
    private static string GetFileFromMapArchive(string entryName)
    {
        return GameMapArchiveManagerAPI.Instance.GetMapArchive()?.TryReadTextFile(entryName) ?? string.Empty;
    }

    /// <summary>
    /// Reads a file from the context root directory (for Lord AI).
    /// </summary>
    private static string GetFileFromContextDirectory(string relativePath)
    {
        if (_contextRootDirectory == null)
        {
            LogHelper.Warning("Context root directory not set for LordAI context");
            return string.Empty;
        }

        try
        {
            // Combine and get the full path
            string fullPath = Path.Combine(_contextRootDirectory, relativePath);

            // Resolve to absolute path to check for traversal
            string resolvedPath = Path.GetFullPath(fullPath);
            string rootPath = Path.GetFullPath(_contextRootDirectory);

            // Security check: ensure the resolved path is within the root directory
            if (!resolvedPath.StartsWith(rootPath, System.StringComparison.OrdinalIgnoreCase))
            {
                LogHelper.Warning($"Path traversal attempt blocked: {relativePath}");
                return string.Empty;
            }

            if (File.Exists(resolvedPath))
            {
                return File.ReadAllText(resolvedPath);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error reading file from context directory: {relativePath}");
        }

        return string.Empty;
    }

    /// <summary>
    /// Reads a file from asset mod sources with fallback priority:
    /// 1. Context root directory (the mod's own files)
    /// 2. Other registered asset mods (in registration order)
    /// </summary>
    internal static string GetFileFromAssetModSources(string relativePath)
    {
        // First, use the calling mod's indexed private namespace.
        if (_contextModGuid != null
            && GameAssetManagerAPI.Instance.GetModFileTextContent(_contextModGuid, relativePath, out string ownContent))
            return ownContent;
        if (_contextModGuid != null
            && !relativePath.StartsWith("Scripts/", StringComparison.OrdinalIgnoreCase)
            && GameAssetManagerAPI.Instance.GetModFileTextContent(_contextModGuid, "Scripts/" + relativePath, out ownContent))
            return ownContent;

        // Compatibility fallback: other registered mods in load order, without filesystem probes.
        foreach (KeyValuePair<ModInfo, string> kvp in GameAssetModManager.Instance.GetRegisteredAssetDirectories())
        {
            if (string.Equals(kvp.Key.GUID, _contextModGuid, StringComparison.OrdinalIgnoreCase))
                continue;
            if (GameAssetManagerAPI.Instance.GetModFileTextContent(kvp.Key.GUID, relativePath, out string content))
                return content;
            if (!relativePath.StartsWith("Scripts/", StringComparison.OrdinalIgnoreCase)
                && GameAssetManagerAPI.Instance.GetModFileTextContent(kvp.Key.GUID, "Scripts/" + relativePath, out content))
                return content;
        }

        return string.Empty;
    }

    /// <summary>
    /// Lists all Lua modules in the map archive.
    /// </summary>
    internal static List<string> ListModulesInMapArchive(string directory)
    {
        List<string> modules = new List<string>();
        MapArchive? archive = GameMapArchiveManagerAPI.Instance.GetMapArchive();

        if (archive?.Archive == null)
            return modules;

        string searchPrefix = string.IsNullOrEmpty(directory) ? "" : directory.Replace("\\", "/") + "/";

        foreach (ICSharpCode.SharpZipLib.Zip.ZipEntry? entry in archive.Archive)
        {
            if (entry == null || !entry.IsFile)
                continue;

            string entryName = entry.Name.Replace("\\", "/");

            if (!entryName.EndsWith(".lua"))
                continue;

            if (!string.IsNullOrEmpty(searchPrefix) && !entryName.StartsWith(searchPrefix))
                continue;

            // Convert to module name: "scripts/utils/helper.lua" -> "scripts.utils.helper"
            string modulePath = entryName[..^4]; // Remove .lua
            string moduleName = modulePath.Replace("/", ".");
            modules.Add(moduleName);
        }

        return modules;
    }

    /// <summary>
    /// Lists all Lua modules in a filesystem directory.
    /// </summary>
    internal static List<string> ListModulesInDirectory(string rootDirectory, string subdirectory)
    {
        List<string> modules = new List<string>();

        try
        {
            string searchPath = string.IsNullOrEmpty(subdirectory) ? rootDirectory : Path.Combine(rootDirectory, subdirectory);
            if (!Directory.Exists(searchPath))
                return modules;

            string[] luaFiles = Directory.GetFiles(searchPath, "*.lua", SearchOption.AllDirectories);
            foreach (string filePath in luaFiles)
            {
                // Get path relative to root
                string relativePath = PathNetCore.GetRelativePath(rootDirectory, filePath);

                // Convert to module name
                string modulePath = relativePath.Replace("\\", "/");
                modulePath = modulePath[..^4]; // Remove .lua
                string moduleName = modulePath.Replace("/", ".");
                modules.Add(moduleName);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error listing modules in directory: {rootDirectory}/{subdirectory}");
        }

        return modules;
    }

    private static List<string> ListModulesInAssetMod(string modGuid, string directory)
    {
        List<string> modules = new List<string>();
        string prefix = string.IsNullOrEmpty(directory) ? string.Empty : directory.Replace('\\', '/').Trim('/') + "/";
        foreach (string path in GameAssetManagerAPI.Instance.GetModFilePaths(modGuid, prefix))
        {
            if (!path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                continue;
            string modulePath = path[..^4];
            modules.Add(modulePath.Replace('/', '.'));
        }
        return modules;
    }
}
