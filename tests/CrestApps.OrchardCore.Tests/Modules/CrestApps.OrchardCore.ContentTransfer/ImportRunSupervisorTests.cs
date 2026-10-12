using System.Collections.Concurrent;
using System.Diagnostics;
using CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Tests.Modules.ContentTransfer;

public sealed class ImportRunSupervisorTests
{
    private const string EntryId = "entry-1";

    // A stuck row is given 300 ms and a run 30 seconds, so a test that hangs on a bug fails instead of hanging.
    private static readonly ImportRunLimits _limits = new(
        RunTimeout: TimeSpan.FromSeconds(30),
        RowTimeout: TimeSpan.FromMilliseconds(300),
        SlowRowThreshold: TimeSpan.FromMilliseconds(100),
        CheckInterval: TimeSpan.FromMilliseconds(20));

    [Fact]
    public async Task RunAsync_ReturnsTheResultOfARunThatFinishes()
    {
        var logger = new ConcurrentLogger();

        var run = await ImportRunSupervisor.RunAsync(
            async (monitor, token) =>
            {
                monitor.BeginRow(1);
                await Task.Delay(10, token);
                monitor.EndRow();

                return true;
            },
            _limits,
            logger,
            EntryId,
            TestContext.Current.CancellationToken);

        Assert.False(run.Abandoned);
        Assert.True(run.Result);
        Assert.Equal(0, run.StalledRowIndex);
        Assert.Empty(logger.At(LogLevel.Error));
    }

    [Fact]
    public async Task RunAsync_AbandonsARunStuckOnARow_WithoutWaitingForIt()
    {
        // The production failure: a row whose work never returned and ignored cancellation, so the run held the
        // import lock until the application restarted.
        var logger = new ConcurrentLogger();
        var neverEnds = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var runToken = CancellationToken.None;
        var started = Stopwatch.GetTimestamp();

        var run = await ImportRunSupervisor.RunAsync(
            (monitor, token) =>
            {
                runToken = token;
                monitor.BeginRow(104_967);

                return neverEnds.Task;
            },
            _limits,
            logger,
            EntryId,
            TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(run.Abandoned);
        Assert.Equal(104_967, run.StalledRowIndex);
        Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(5));

        // Cancelled, so if the run ever wakes up it cannot save progress over the run that replaces it.
        Assert.True(runToken.IsCancellationRequested);
        Assert.Contains(logger.At(LogLevel.Error), message => message.Contains("104967") && message.Contains(EntryId));

        neverEnds.SetResult(false);
    }

    [Fact]
    public async Task RunAsync_AbandonsARunBlockedInSynchronousWork()
    {
        using var release = new ManualResetEventSlim();

        try
        {
            var run = await ImportRunSupervisor.RunAsync(
                (monitor, token) =>
                {
                    monitor.BeginRow(12);

                    // Blocks the thread instead of awaiting, as a call made synchronously would.
                    release.Wait(TimeSpan.FromSeconds(20), CancellationToken.None);

                    return Task.FromResult(true);
                },
                _limits,
                new ConcurrentLogger(),
                EntryId,
                TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            Assert.True(run.Abandoned);
            Assert.Equal(12, run.StalledRowIndex);
        }
        finally
        {
            release.Set();
        }
    }

    [Fact]
    public async Task RunAsync_AbandonsARunPastItsTimeout_EvenBetweenRows()
    {
        var logger = new ConcurrentLogger();
        var neverEnds = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var limits = _limits with { RunTimeout = TimeSpan.FromMilliseconds(300) };

        var run = await ImportRunSupervisor.RunAsync(
            (_, _) => neverEnds.Task,
            limits,
            logger,
            EntryId,
            TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Between rows nothing is to blame, so no row is recorded against the file.
        Assert.True(run.Abandoned);
        Assert.Equal(0, run.StalledRowIndex);
        Assert.Single(logger.At(LogLevel.Error));

        neverEnds.SetResult(false);
    }

    [Fact]
    public async Task RunAsync_WarnsOnceAboutASlowRow_ThatThenFinishes()
    {
        var logger = new ConcurrentLogger();
        var limits = _limits with { RowTimeout = TimeSpan.FromSeconds(30) };

        var run = await ImportRunSupervisor.RunAsync(
            async (monitor, token) =>
            {
                monitor.BeginRow(5);
                await Task.Delay(400, token);
                monitor.EndRow();

                return false;
            },
            limits,
            logger,
            EntryId,
            TestContext.Current.CancellationToken);

        Assert.False(run.Abandoned);
        Assert.Single(logger.At(LogLevel.Warning), message => message.Contains("row 5"));
        Assert.Empty(logger.At(LogLevel.Error));
    }

    [Fact]
    public async Task RunAsync_ReturnsWhenTheHostStops_WhileARunIgnoresIt()
    {
        using var hostStopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var neverEnds = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var limits = _limits with { RowTimeout = TimeSpan.FromSeconds(30) };

        var supervising = ImportRunSupervisor.RunAsync(
            (monitor, _) =>
            {
                monitor.BeginRow(3);

                return neverEnds.Task;
            },
            limits,
            new ConcurrentLogger(),
            EntryId,
            hostStopping.Token);

        await Task.Delay(50, TestContext.Current.CancellationToken);
        await hostStopping.CancelAsync();

        var run = await supervising.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // A stopping host is not the row's fault, so the row is not counted against the file.
        Assert.True(run.Abandoned);
        Assert.Equal(0, run.StalledRowIndex);

        neverEnds.SetResult(false);
    }

    [Fact]
    public async Task RunAsync_PassesOnTheFailureOfARunThatFails()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => ImportRunSupervisor.RunAsync<bool>(
            (_, _) => throw new InvalidOperationException("The run failed."),
            _limits,
            new ConcurrentLogger(),
            EntryId,
            TestContext.Current.CancellationToken));
    }

    private sealed class ConcurrentLogger : ILogger
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

        public List<string> At(LogLevel level)
            => _entries.Where(entry => entry.Level == level).Select(entry => entry.Message).ToList();

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            => _entries.Enqueue((logLevel, formatter(state, exception)));
    }
}
