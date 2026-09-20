using SHCDESE.Logging;
using System;
using System.Diagnostics;
using System.Text;

namespace SHCDESE.Interop;

public class CallstackHelper
{
    /// <summary>
    /// Prints the calling method call stack up to the specified depth.
    /// </summary>
    /// <param name="maxDepth">Maximum number of stack frames to print.</param>
    public static void PrintCallStack(int maxDepth = 5)
    {
        var stackTrace = new StackTrace(skipFrames: 1, fNeedFileInfo: true);
        var frames = stackTrace.GetFrames();

        if (frames == null || frames.Length == 0)
        {
            LogHelper.Warning("Call stack is empty.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Call stack:");

        int depth = Math.Min(maxDepth, frames.Length);

        for (int i = 0; i < depth; i++)
        {
            var frame = frames[i];
            var method = frame.GetMethod();

            sb.AppendLine(
                $"  #{i + 1}: {method?.DeclaringType?.FullName}.{method?.Name} " +
                $"(Line {frame.GetFileLineNumber()})"
            );
        }

        LogHelper.Information(sb.ToString());
    }
}
