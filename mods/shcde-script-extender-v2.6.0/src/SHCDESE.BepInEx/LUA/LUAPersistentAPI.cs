using NLua;
using SHCDESE.Extensions;
using SHCDESE.IO;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.IO;

namespace SHCDESE.Lua;

/// <summary>
/// Exposes a file-based persistence system to the Lua scripting environment,
/// allowing scripts to save and load Lua tables as JSON files in a sandboxed directory.
/// </summary>
[LuaApiNamespace("Persistent")]
public static class LuaPersistentAPI
{
    /// <summary>
    /// Registers all persistent storage functions with the specified Lua instance.
    /// </summary>
    /// <param name="lua">The Lua state to register the functions into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedStaticMethods(typeof(LuaPersistentAPI));
    }
    
    // technically you could pass through .exe or .dll filepaths here, but there is no way to
    // actually write code into them through lua.
    private static bool IsValidPersistentPath(string persistentFileName, out string? finalPath)
    {
        finalPath = string.Empty;
        string baseFolder = DirectoryHelpers.GamePersistentDirectory;

        // Normalize input, remove dangerous characters and whitespace
        persistentFileName = persistentFileName.Trim();

        // Reject absolute paths
        if (Path.IsPathRooted(persistentFileName))
        {
            LogHelper.Error($"Absolute filepaths are not allowed.");
            return false;
        }

        // Combine with base folder
        string combinedPath = Path.Combine(baseFolder, persistentFileName);

        // Resolve to absolute real path
        string fullBase = Path.GetFullPath(baseFolder);
        string fullTarget = Path.GetFullPath(combinedPath);

        // Ensure target is within base folder
        if (!fullTarget.StartsWith(fullBase, StringComparison.InvariantCultureIgnoreCase))
        {
            LogHelper.Error($"Path escapes base directory or is otherwise non-conform.");
            return false;
        }
        Directory.CreateDirectory(fullBase);
        finalPath = fullTarget;

        return true;
    }

    /// <summary>
    /// (For Lua) Serializes a Lua table to a JSON string and saves it to a file within the script extender's persistent storage directory.
    /// </summary>
    /// <param name="persistentFileName">The name of the file (e.g., "myConfig.json"). This is a relative path; directory traversal is not allowed.</param>
    /// <param name="table">The Lua table to save.</param>
    /// <param name="doOverwrite">If true, an existing file with the same name will be overwritten. Defaults to true.</param>
    /// <returns><c>true</c> if the save was successful; otherwise, <c>false</c>.</returns>
    [LuaApiExport("SaveTableAsJson")]
    public static bool SaveTableAsJson(string persistentFileName, LuaTable table, bool doOverwrite = true)
    {
        try
        {
            if (!IsValidPersistentPath(persistentFileName, out string? finalPath) && !string.IsNullOrEmpty(finalPath))
            {
                LogHelper.Error($"Path is invalid.");
                return false;
            }

            if (!doOverwrite && File.Exists(finalPath))
            {
                LogHelper.Error($"File already exists and overwrite is disabled: {persistentFileName}");
                return false;
            }

            string json = LuaJson.LuaTableToJson(table);
            File.WriteAllText(finalPath, json);
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Exception during save");
            return false;
        }
    }

    /// <summary>
    /// (For Lua) Loads a JSON file from the script extender's persistent storage directory and deserializes it into a new Lua table.
    /// </summary>
    /// <param name="persistentFileName">The name of the file to load (e.g., "myConfig.json").</param>
    /// <returns>A new LuaTable containing the data from the file on success; otherwise, <c>null</c> (which becomes `nil` in Lua).</returns>
    [LuaApiExport("LoadTableFromJson")]
    public static LuaTable LoadTableFromJson(string persistentFileName)
    {
        try
        {
            if (!IsValidPersistentPath(persistentFileName, out string? finalPath) && !string.IsNullOrEmpty(finalPath))
            {
                LogHelper.Error($"Path is invalid.");
                return null;
            }

            if (LuaManager.Instance.Lua == null)
            {
                LogHelper.Error($"LUA is null! (How?)");
                return null;
            }

            string json = File.ReadAllText(finalPath);
            return LuaJson.JsonToLuaTable(LuaManager.Instance.Lua, json);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Exception during save");
            return null;
        }
    }
}