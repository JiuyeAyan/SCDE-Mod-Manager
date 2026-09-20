using BepInEx.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.BepInEx.Logging;
using SHCDESE.Logging;
using System;
using System.Collections.Concurrent;
using bepLogging = BepInEx.Logging;

namespace SHCDESE.API.Logging;

/// <summary>
/// Factory for creating isolated loggers for mods.
/// Each mod gets its own logger with its own prefix in the logs.
/// </summary>
public class ModLoggerFactory
{
    private static readonly ConcurrentDictionary<string, ILogger> _loggers = new ConcurrentDictionary<string, ILogger>();
    private static LoggingLevelSwitch _globalLevelSwitch;

    /// <summary>
    /// Initializes the mod logger factory with the global log level switch.
    /// Called internally by the script extender during startup.
    /// </summary>
    internal static void Initialize(LoggingLevelSwitch globalSwitch)
    {
        _globalLevelSwitch = globalSwitch;
    }

    /// <summary>
    /// Creates or retrieves a logger for a mod.
    /// </summary>
    /// <param name="modName">The name of your mod (e.g., "MyAwesomeMod"). This will appear in log outputs.</param>
    /// <returns>A Serilog ILogger instance configured for your mod.</returns>
    public static ILogger GetLogger(string modName)
    {
        if (string.IsNullOrWhiteSpace(modName))
            throw new ArgumentException("Mod name cannot be null or empty", nameof(modName));

        return _loggers.GetOrAdd(modName, name =>
        {
            // Create a BepInEx log source for this mod
            ManualLogSource logSource = bepLogging.Logger.CreateLogSource(name);

            // Create a Serilog logger that writes to this mod's log source
            LoggerConfiguration loggerConfig = new Serilog.LoggerConfiguration()
                .MinimumLevel.ControlledBy(_globalLevelSwitch ?? new LoggingLevelSwitch { MinimumLevel = LogEventLevel.Information })
                .Enrich.FromLogContext();
            if (Plugin.Instance.LogThreadIds.Value)
                loggerConfig.Enrich.With(new ThreadIdEnricher());

            if (Plugin.Instance.AsyncLogging.Value)
                loggerConfig.WriteTo.Async(a => a.Sink(new BepInExSink(
                    logSource,
                    Plugin.Instance.LogTimestamps.Value,
                    Plugin.Instance.LogThreadIds.Value)));
            else
                loggerConfig.WriteTo.Sink(new BepInExSink(
                    logSource,
                    Plugin.Instance.LogTimestamps.Value,
                    Plugin.Instance.LogThreadIds.Value));

            Serilog.Core.Logger logger = loggerConfig.CreateLogger();

            return logger;
        });
    }

    /// <summary>
    /// Creates a ModLogHelper instance for easier logging with caller info.
    /// This is the recommended approach for mod developers.
    /// </summary>
    /// <param name="modName">The name of your mod.</param>
    /// <returns>A ModLogHelper instance.</returns>
    public static ModLogHelper CreateHelper(string modName)
    {
        return new ModLogHelper(GetLogger(modName));
    }
}
