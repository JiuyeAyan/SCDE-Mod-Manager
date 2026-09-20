using BepInEx;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace SHCDESE.NativeHooks;

/// <summary>
/// Configures the native crash-handler DLL. The vectored exception handler, its callback, stack walk, logging, and minidump generation are all implemented in native code.
/// </summary>
internal static class NativeCrashHandler
{
    private const string LibraryName = "SHCDESE.NativeCrashHandler.dll";
    private const string CrashDumpDirectoryName = "crashdumps";

    [DllImport(LibraryName, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
    private static extern uint SHCDECrashHandler_Install(string bepInExLogPath, string dumpDirectory);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
    private static extern uint SHCDECrashHandler_Uninstall();

    internal static string Install()
    {
        string logPath = Path.Combine(Paths.BepInExRootPath, "LogOutput.log");
        string dumpDirectory = GetCrashDumpDirectory();
        uint result = SHCDECrashHandler_Install(logPath, dumpDirectory);
        if (result != 0)
            throw new Win32Exception(unchecked((int)result), "Could not install the native crash handler");

        return dumpDirectory;
    }

    /// <summary>
    /// Deletes the oldest files from the native crash-dump directory until at most
    /// <paramref name="maxFiles"/> files remain.
    /// </summary>
    /// <param name="maxFiles">Maximum number of files to retain. Defaults to five.</param>
    /// <returns>The number of files deleted.</returns>
    internal static int TrimCrashDumps(int maxFiles = 5)
    {
        if (maxFiles < 0)
            throw new ArgumentOutOfRangeException(nameof(maxFiles), maxFiles, "The crash-dump file limit cannot be negative.");

        string dumpDirectory = GetCrashDumpDirectory();
        if (!Directory.Exists(dumpDirectory))
            return 0;

        FileInfo[] files = new DirectoryInfo(dumpDirectory).GetFiles();
        int filesToDelete = files.Length - maxFiles;
        if (filesToDelete <= 0)
            return 0;

        Array.Sort(files, static (left, right) =>
        {
            int dateComparison = left.LastWriteTimeUtc.CompareTo(right.LastWriteTimeUtc);
            return dateComparison != 0 ? dateComparison : StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
        });

        for (int index = 0; index < filesToDelete; index++)
            files[index].Delete();

        return filesToDelete;
    }

    internal static void Uninstall()
    {
        uint result = SHCDECrashHandler_Uninstall();
        if (result != 0)
            throw new Win32Exception(unchecked((int)result), "Could not uninstall the native crash handler");
    }

    private static string GetCrashDumpDirectory() => Path.Combine(Paths.BepInExRootPath, CrashDumpDirectoryName);
}

