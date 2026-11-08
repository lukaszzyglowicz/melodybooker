using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace melody.Tests;

/// <summary>
/// A single captured log call: category, level, formatted message, and the exception (if any)
/// passed to the logging call.
/// </summary>
public record CapturedLogEntry(LogLevel Level, string Category, string Message, Exception? Exception);

/// <summary>
/// A test-only <see cref="ILoggerProvider"/> that records every log call into a thread-safe list
/// instead of writing anywhere real, so tests can assert on what a controller logged.
/// </summary>
public class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();

    public IReadOnlyList<CapturedLogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    private class CapturingLogger(string categoryName, ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(new CapturedLogEntry(logLevel, categoryName, formatter(state, exception), exception));
        }
    }
}
