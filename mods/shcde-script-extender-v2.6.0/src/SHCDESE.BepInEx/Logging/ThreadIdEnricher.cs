using Serilog.Core;
using Serilog.Events;
using System.Threading;

namespace SHCDESE.Logging;

/// <summary>
/// Captures the managed thread ID while the log event is created, before an asynchronous sink can move it onto its worker thread.
/// </summary>
internal sealed class ThreadIdEnricher : ILogEventEnricher
{
    internal const string PropertyName = "ThreadId";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            PropertyName,
            Thread.CurrentThread.ManagedThreadId));
    }
}
