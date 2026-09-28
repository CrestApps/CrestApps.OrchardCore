using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A logger that keeps what it is given, for tests that pin a log line production troubleshooting depends on.
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    /// <summary>Gets every entry written, in order.</summary>
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    /// <summary>The messages written at <paramref name="level"/>.</summary>
    public IEnumerable<string> At(LogLevel level)
        => Entries.Where(entry => entry.Level == level).Select(entry => entry.Message);

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        => Entries.Add((logLevel, formatter(state, exception)));
}
