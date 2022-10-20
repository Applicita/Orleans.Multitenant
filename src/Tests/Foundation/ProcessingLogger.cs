using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OrleansMultitenant.Tests;

public static class LoggingBuilderExtensions
{
    public static ILoggingBuilder AddProcessing(this ILoggingBuilder builder)
     => builder.AddProvider(new ProcessingLoggerProvider());
}

sealed class ProcessingLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => ProcessingLogger.Instance;

    public void Dispose() { }
}

sealed class ProcessingLogger : ILogger
{
    /// <remarks>static can be used to access the same object instances in silo's and tests, because <see cref="Orleans.TestingHost.TestCluster"/> uses in-process silo's</remarks>
    internal static ProcessingLogger Instance { get; } = new();

    readonly ConcurrentDictionary<Guid, Action<LogLevel, Exception?, string>> logEventProcessors = new();

    internal Guid AddLogEventProcessor(Action<LogLevel, Exception?, string> processor)
    {
        var id = Guid.NewGuid();
        logEventProcessors[id] = processor;
        return id;
    }

    internal bool RemoveLogEventProcessor(Guid id) => logEventProcessors.Remove(id, out _);

    IDisposable ILogger.BeginScope<TState>(TState state) => new NopDisposable();

    bool ILogger.IsEnabled(LogLevel logLevel) => true;

    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        foreach ((_, var logProcessor) in logEventProcessors)
            logProcessor(logLevel, exception, formatter(state, exception));
    }

    sealed class NopDisposable : IDisposable { void IDisposable.Dispose() { } }
}
