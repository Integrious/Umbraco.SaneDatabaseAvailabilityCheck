using Microsoft.Extensions.Logging;

namespace Integrious.Umbraco.SaneDatabaseAvailabilityCheck.Tests;

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/> that keeps what was logged, so tests can assert on levels
/// as well as on the fact that something was said.
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IEnumerable<string> MessagesAt(LogLevel level) =>
        Entries.Where(e => e.Level == level).Select(e => e.Message);

    public int CountAt(LogLevel level) => Entries.Count(e => e.Level == level);

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull => NullScope.Instance;

    // Debug included: the check only writes its first-failure message when Debug is enabled.
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
