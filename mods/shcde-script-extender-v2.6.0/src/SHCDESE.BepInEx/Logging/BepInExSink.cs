using BepInEx.Logging;
using Serilog.Core;
using Serilog.Events;
using SHCDESE.API;
using SHCDESE.Logging;
using System;
using System.Globalization;

namespace SHCDESE.BepInEx.Logging;

public class BepInExSink : ILogEventSink
{
    private readonly ManualLogSource _logSource;
    private readonly bool _includeTimestamp;
    private readonly bool _includeThreadId;
    private readonly object _lock = new object();

    public BepInExSink(ManualLogSource logSource)
        : this(logSource, false, false)
    {
    }

    public BepInExSink(ManualLogSource logSource, bool includeTimestamp, bool includeThreadId)
    {
        _logSource = logSource ?? throw new ArgumentNullException(nameof(logSource));
        _includeTimestamp = includeTimestamp;
        _includeThreadId = includeThreadId;
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent == null)
            return;

        // Try to get known properties
        logEvent.Properties.TryGetValue("ClassName", out LogEventPropertyValue? classProp);
        logEvent.Properties.TryGetValue("MethodName", out LogEventPropertyValue? methodProp);
        logEvent.Properties.TryGetValue("SourceContext", out LogEventPropertyValue? sourceContext);
        logEvent.Properties.TryGetValue(ThreadIdEnricher.PropertyName, out LogEventPropertyValue? threadIdProp);

        string className =
            classProp?.ToString().Trim('"')
            ?? sourceContext?.ToString().Trim('"')
            ?? "Unknown";

        // Render the message now
        string renderedMessage = logEvent.RenderMessage();

        string callerPrefix =
            methodProp != null
                ? $"[{className}] [{methodProp.ToString().Trim('"')}] "
                : $"[{className}] ";

        string timestamp = _includeTimestamp
            ? logEvent.Timestamp
                .ToLocalTime()
                .ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
            : string.Empty;

        string threadId = _includeThreadId
            ? (threadIdProp is ScalarValue scalar
                ? Convert.ToString(scalar.Value, CultureInfo.InvariantCulture) ?? "?"
                : threadIdProp?.ToString() ?? "?")
            : string.Empty;

        string metadataPrefix;
        if (_includeTimestamp && _includeThreadId)
            metadataPrefix = $"[T{threadId}-{timestamp}] ";
        else if (_includeThreadId)
            metadataPrefix = $"[T{threadId}] ";
        else if (_includeTimestamp)
            metadataPrefix = $"[{timestamp}] ";
        else
            metadataPrefix = string.Empty;

        string finalMessage = metadataPrefix + callerPrefix + renderedMessage;

        // Capture other immutable data needed for the write
        //LogEventLevel level = logEvent.Level;
        //Exception? exception = logEvent.Exception;

        // Dispatch the actual writing to the Unity Main Thread.
        //UnityMainThreadDispatcher.Dispatch(() =>
        //{
        //    WriteToLogSource(level, finalMessage, exception);
        //});
        lock (_lock)
        {
            WriteToLogSource(logEvent.Level, finalMessage, logEvent.Exception, metadataPrefix + callerPrefix);
        }
    }

    private void WriteToLogSource(LogEventLevel level, string message, Exception? ex, string exceptionPrefix)
    {
        switch (level)
        {
            case LogEventLevel.Verbose:
            case LogEventLevel.Debug:
                _logSource.LogDebug(message);
                break;

            case LogEventLevel.Information:
                _logSource.LogInfo(message);
                break;

            case LogEventLevel.Warning:
                _logSource.LogWarning(message);
                break;

            case LogEventLevel.Error:
                _logSource.LogError(message);
                break;

            case LogEventLevel.Fatal:
                _logSource.LogFatal(message);
                break;
        }

        if (ex != null)
        {
            _logSource.LogError(exceptionPrefix + ex);
        }
    }
}
