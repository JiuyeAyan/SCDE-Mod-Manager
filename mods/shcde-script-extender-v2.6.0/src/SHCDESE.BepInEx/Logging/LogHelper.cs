using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace SHCDESE.Logging;

public static class LogHelper
{
    [Conditional("DEBUG")]
    public static void Verbose(string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        if (!Log.IsEnabled(Serilog.Events.LogEventLevel.Verbose))
            return;

        string className = Path.GetFileNameWithoutExtension(filePath);
        Log
            .ForContext("ClassName", className)
            .ForContext("MethodName", memberName)
            .Verbose(message);
    }

    [Conditional("DEBUG")]
    public static void Debug(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "")
    {
        if (!Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            return;

        string className = Path.GetFileNameWithoutExtension(filePath);
        Log
            .ForContext("ClassName", className)
            .ForContext("MethodName", memberName)
            .Debug(message);
    }

    public static void Information(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "")
    {
        if (!Log.IsEnabled(Serilog.Events.LogEventLevel.Information))
            return;

        string className = Path.GetFileNameWithoutExtension(filePath);
        Log
            .ForContext("ClassName", className)
            .ForContext("MethodName", memberName)
            .Information(message);
    }

    public static void Warning(string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        if (!Log.IsEnabled(Serilog.Events.LogEventLevel.Warning))
            return;

        string className = Path.GetFileNameWithoutExtension(filePath);
        Log
            .ForContext("ClassName", className)
            .ForContext("MethodName", memberName)
            .Warning(message);
    }

    public static void Error(string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        if (!Log.IsEnabled(Serilog.Events.LogEventLevel.Error))
            return;

        string className = Path.GetFileNameWithoutExtension(filePath);
        Log
            .ForContext("ClassName", className)
            .ForContext("MethodName", memberName)
            .Error(message);
    }

    public static void Error(Exception ex, string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        if (!Log.IsEnabled(Serilog.Events.LogEventLevel.Error))
            return;

        string className = Path.GetFileNameWithoutExtension(filePath);
        Log
            .ForContext("ClassName", className)
            .ForContext("MethodName", memberName)
            .Error(ex, message);
    }

    public static void Fatal(string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        if (!Log.IsEnabled(Serilog.Events.LogEventLevel.Fatal))
            return;

        string className = Path.GetFileNameWithoutExtension(filePath);
        Log
            .ForContext("ClassName", className)
            .ForContext("MethodName", memberName)
            .Fatal(message);
    }

    public static void Fatal(Exception ex, string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        if (!Log.IsEnabled(Serilog.Events.LogEventLevel.Fatal))
            return;

        string className = Path.GetFileNameWithoutExtension(filePath);
        Log
            .ForContext("ClassName", className)
            .ForContext("MethodName", memberName)
            .Fatal(ex, message);
    }
}