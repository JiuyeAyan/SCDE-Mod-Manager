using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
namespace SHCDESE.IO;

public class DirectoryHelpers
{
    internal const string DEFAULT_PERSISTENT_FOLDER_NAME = "_SE_PERSISTENT";

    private static string gameAppDataDirectory = string.Empty;
    public static string GameAppDataDirectory
    {
        get
        {
            if (gameAppDataDirectory == string.Empty)
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                gameAppDataDirectory = Path.Combine(userProfile, "AppData", "LocalLow", "Firefly Studios", "Stronghold Crusader Definitive Edition");
            }
            return gameAppDataDirectory;
        }
    }

    private static string gameMapsDirectory = string.Empty;
    public static string GameMapsDirectory
    {
        get
        {
            if (gameMapsDirectory == string.Empty)
            {
                gameMapsDirectory = Path.Combine(GameAppDataDirectory, "Maps");
            }
            return gameMapsDirectory;
        }
    }

    private static string gamePersistentDirectory = string.Empty;
    public static string GamePersistentDirectory
    {
        get
        {
            if (gamePersistentDirectory == string.Empty)
            {
                gamePersistentDirectory = Path.Combine(GameAppDataDirectory, DEFAULT_PERSISTENT_FOLDER_NAME);
            }
            return gamePersistentDirectory;
        }
    }

    private static string modDirectory = string.Empty;
    public static string ModDirectory
    {
        get
        {
            if (modDirectory == string.Empty)
            {
                modDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }
            return modDirectory;
        }
    }

    private static string modDataDirectory = string.Empty;
    public static string ModDataDirectory
    {
        get
        {
            if (modDataDirectory == string.Empty)
            {
                modDataDirectory = Path.Combine(ModDirectory, "data");
            }
            return modDataDirectory;
        }
    }

    private static string gameDirectory = string.Empty;
    public static string GameDirectory
    {
        get
        {
            if (gameDirectory == string.Empty)
            {
                gameDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            }
            return gameDirectory;
        }
    }

    private static string gameDataDirectory = string.Empty;
    public static string GameDataDirectory
    {
        get
        {
            if (gameDataDirectory == string.Empty)
            {
                gameDataDirectory = Path.Combine(GameDirectory, "Stronghold Crusader Definitive Edition_Data");
            }
            return gameDataDirectory;
        }
    }

    private static string gameStreamingAssetsDirectory = string.Empty;
    public static string GameStreamingAssetsDirectory
    {
        get
        {
            if (gameStreamingAssetsDirectory == string.Empty)
            {
                gameStreamingAssetsDirectory = Path.Combine(GameDataDirectory, "StreamingAssets");
            }
            return gameStreamingAssetsDirectory;
        }
    }

    private static string bepInExDirectory = string.Empty;
    public static string BepInExDirectory
    {
        get
        {
            if (bepInExDirectory == string.Empty)
            {
                bepInExDirectory = Path.Combine(GameDirectory, "BepInEx");
            }
            return bepInExDirectory;
        }
    }

    private static string bepInExPluginsDirectory = string.Empty;
    public static string BepInExPluginsDirectory
    {
        get
        {
            if (bepInExPluginsDirectory == string.Empty)
            {
                bepInExPluginsDirectory = Path.Combine(GameDirectory, BepInExDirectory, "plugins");
            }
            return bepInExPluginsDirectory;
        }
    }

    /// <summary>
    /// Copies one directory to another one recursively.
    /// </summary>
    /// <param name="sourcePath">Source directory</param>
    /// <param name="destinationPath">Target directory</param>
    /// <param name="overwrite">Whether to overwrite files</param>
    /// <exception cref="DirectoryNotFoundException"></exception>
    public static void CopyDirectory(string sourcePath, string destinationPath, bool overwrite = true)
    {
        DirectoryInfo dir = new DirectoryInfo(sourcePath);

        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");

        Directory.CreateDirectory(destinationPath);
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationPath, file.Name);
            file.CopyTo(targetFilePath, overwrite);
        }

        foreach (DirectoryInfo subDir in dir.GetDirectories())
        {
            string newDestinationDir = Path.Combine(destinationPath, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir, overwrite);
        }
    }
}
