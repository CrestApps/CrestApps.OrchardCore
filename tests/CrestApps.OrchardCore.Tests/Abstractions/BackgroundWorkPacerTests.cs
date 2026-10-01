using System.Diagnostics;
using CrestApps.OrchardCore.BackgroundWork;

namespace CrestApps.OrchardCore.Tests.Abstractions;

[CollectionDefinition(nameof(BackgroundWorkGateCollection), DisableParallelization = true)]
public sealed class BackgroundWorkGateCollection;

// The gate is shared by the whole process, so these tests do not run alongside the other tests that use it.
[Collection(nameof(BackgroundWorkGateCollection))]
public sealed class BackgroundWorkPacerTests
{
    private static readonly BackgroundWorkPacingOptions _noPause = new() { DatabaseShare = 1 };

    [Fact]
    public void GetPause_WithTheDefaultShare_WaitsThreeTimesTheBatch()
    {
        var pause = BackgroundWorkPacer.GetPause(TimeSpan.FromSeconds(1), new BackgroundWorkPacingOptions());

        Assert.Equal(TimeSpan.FromSeconds(3), pause);
    }

    [Fact]
    public void GetPause_WithoutOptions_UsesTheDefaults()
    {
        var pause = BackgroundWorkPacer.GetPause(TimeSpan.FromMilliseconds(200), null);

        Assert.Equal(TimeSpan.FromMilliseconds(600), pause);
    }

    [Fact]
    public void GetPause_WithAFullShare_DoesNotWait()
    {
        var pause = BackgroundWorkPacer.GetPause(TimeSpan.FromSeconds(5), _noPause);

        Assert.Equal(TimeSpan.Zero, pause);
    }

    [Fact]
    public void GetPause_NeverExceedsTheMaximum()
    {
        var options = new BackgroundWorkPacingOptions { DatabaseShare = 0.1, MaxPause = TimeSpan.FromSeconds(10) };

        var pause = BackgroundWorkPacer.GetPause(TimeSpan.FromSeconds(5), options);

        Assert.Equal(TimeSpan.FromSeconds(10), pause);
    }

    [Fact]
    public void GetPause_RaisesATooSmallShareToTheMinimum()
    {
        var pause = BackgroundWorkPacer.GetPause(TimeSpan.FromSeconds(1), new BackgroundWorkPacingOptions { DatabaseShare = 0 });

        // At the 0.05 minimum a one-second batch is followed by a 19-second pause, not an endless one.
        Assert.Equal(TimeSpan.FromSeconds(19), pause);
    }

    [Fact]
    public async Task RunBatchAsync_RunsOneBatchAtATime()
    {
        Assert.Equal(1, await MeasureMostBatchesAtOnceAsync());
    }

    [Fact]
    public async Task RunBatchAsync_KeepsTheGateThroughThePause()
    {
        var options = new BackgroundWorkPacingOptions { DatabaseShare = 0.5 };
        var starts = new long[2];

        await Task.WhenAll(Enumerable.Range(0, 2).Select(i => BackgroundWorkPacer.RunBatchAsync(
            async token =>
            {
                starts[i] = Stopwatch.GetTimestamp();
                await Task.Delay(100, token);

                return true;
            },
            options,
            TestContext.Current.CancellationToken)));

        // A 100 ms batch at half the database is followed by a 100 ms pause, and the next batch waits for both.
        var gap = Stopwatch.GetElapsedTime(Math.Min(starts[0], starts[1]), Math.Max(starts[0], starts[1]));
        Assert.True(gap >= TimeSpan.FromMilliseconds(180), $"The second batch started {gap.TotalMilliseconds} ms after the first.");
    }

    [Fact]
    public async Task RunBatchAsync_ReleasesTheGateWhenTheBatchThrows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => BackgroundWorkPacer.RunBatchAsync<bool>(
            _ => throw new InvalidOperationException("The batch failed."),
            _noPause,
            TestContext.Current.CancellationToken));

        // A gate left held would make every later batch wait out the limit and then run together.
        Assert.Equal(1, await MeasureMostBatchesAtOnceAsync());
    }

    [Fact]
    public async Task RunBatchAsync_ReleasesTheGateWhenCancelledDuringThePause()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var slowShare = new BackgroundWorkPacingOptions { DatabaseShare = 0.05 };

        var run = BackgroundWorkPacer.RunBatchAsync(
            async token =>
            {
                await Task.Delay(50, token);

                return true;
            },
            slowShare,
            cancellation.Token);

        // The batch is over and the job is in its pause of about a second when the host stops.
        await Task.Delay(150, TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal(1, await MeasureMostBatchesAtOnceAsync());
    }

    [Fact]
    public async Task RunBatchAsync_RunsWithoutTheGateWhenABatchHoldsItTooLong()
    {
        var hungBatch = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hung = BackgroundWorkPacer.RunBatchAsync(_ => hungBatch.Task, _noPause, TestContext.Current.CancellationToken);

        try
        {
            var ran = await BackgroundWorkPacer.RunBatchAsync(
                _ => Task.FromResult(true),
                _noPause,
                TimeSpan.FromMilliseconds(200),
                TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(ran);
        }
        finally
        {
            hungBatch.SetResult(true);
            await hung;
        }

        Assert.Equal(1, await MeasureMostBatchesAtOnceAsync());
    }

    // Runs several short batches at once and reports the most that were inside a batch at the same moment.
    private static async Task<int> MeasureMostBatchesAtOnceAsync()
    {
        var running = 0;
        var most = 0;

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => BackgroundWorkPacer.RunBatchAsync(
            async token =>
            {
                var now = Interlocked.Increment(ref running);
                InterlockedMax(ref most, now);
                await Task.Delay(30, token);
                Interlocked.Decrement(ref running);

                return true;
            },
            _noPause,
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken)));

        return most;
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;

        while (value > (current = Volatile.Read(ref target)))
        {
            if (Interlocked.CompareExchange(ref target, value, current) == current)
            {
                return;
            }
        }
    }
}
