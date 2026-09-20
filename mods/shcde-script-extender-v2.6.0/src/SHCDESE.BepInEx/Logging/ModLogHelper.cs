using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace SHCDESE.API.Logging;

/// <summary>
/// A convenient logging helper for mod developers.
/// Provides automatic caller information (class name and method name) like the internal LogHelper.
/// </summary>
public class ModLogHelper
{
    private readonly ILogger _logger;

    internal ModLogHelper(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Conditional("DEBUG")]
    public void Verbose(string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        if (!_logger.IsEnabled(Serilog.Events.LogEventLevel.Verbose))
            return;

        string className = Path.GetFileNameWithoutExtension(filePath);
        _logger
            .ForContext("ClassName", className)
            .ForContext("MethodName", memberName)
            .Verbose(message);
    }

    [Conditional("DEBUG")]
    public void Debug(string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        if (!_logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            return;

        string className = Path.GetFileNameWithoutExtension(filePath);
        _logger
            .ForContext("ClassName", className)
            .ForContext("MethodName", memberName)
            .Debug(message);
    }

    public void Information(string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        if (!_logger.IsEnabled(Serilog.Events.LogEventLevel.Information))
            return;

        string className = Path.GetFileNameWithoutExtension(filePath);
        _logger
            .ForContext("ClassName", className)
            .ForContext("MethodName", memberName)
            .Information(message);
    }

    public void Warning(string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        if (!_logger.IsEnabled(Serilog.Events.LogEventLevel.Warning))
            return;

        string className = Path.GetFileNameWithoutExtension(filePath);
        _logger
            .ForContext("ClassName", className)
            .ForContext("MethodName", memberName)
            .Warning(message);
    }

    public void Error(string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        if (!_logger.IsEnabled(Serilog.Events.LogEventLevel.Error))
            return;

        string className = Path.GetFileNameWithoutExtension(filePath);
        _logger
            .ForContext("ClassName", className)
            .ForContext("MethodName", memberName)
            .Error(message);
    }

    public void Error(Exception ex, string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        if (!_logger.IsEnabled(Serilog.Events.LogEventLevel.Error))
            return;

        string className = Path.GetFileNameWithoutExtension(filePath);
        _logger
            .ForContext("ClassName", className)
            .ForContext("MethodName", memberName)
            .Error(ex, message);
    }

    public void Fatal(string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        if (!_logger.IsEnabled(Serilog.Events.LogEventLevel.Fatal))
            return;

        string className = Path.GetFileNameWithoutExtension(filePath);
        _logger
            .ForContext("ClassName", className)
            .ForContext("MethodName", memberName)
            .Fatal(message);
    }

    public void Fatal(Exception ex, string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        if (!_logger.IsEnabled(Serilog.Events.LogEventLevel.Fatal))
            return;

        string className = Path.GetFileNameWithoutExtension(filePath);
        _logger
            .ForContext("ClassName", className)
            .ForContext("MethodName", memberName)
            .Fatal(ex, message);
    }

    /// <summary>
    /// Gets the underlying Serilog ILogger for advanced scenarios.
    /// Most mod developers won't need this.
    /// </summary>
    public ILogger GetLogger() => _logger;
}