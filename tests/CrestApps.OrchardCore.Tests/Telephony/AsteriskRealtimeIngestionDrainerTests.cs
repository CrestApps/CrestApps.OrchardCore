using System.Threading.Channels;
using CrestApps.OrchardCore.Asterisk.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskRealtimeIngestionDrainerTests
{
    [Fact]
    public async Task DrainAsync_WhenTheWorkerDrainsAndCompletes_ReturnsWithoutCancellingTheWorker()
    {
        // Arrange
        var channel = CreateChannel(3);
        channel.Writer.Complete();

        using var workerCts = new CancellationTokenSource();
        var processed = new int[1];
        var worker = StartReadingWorker(channel, TimeSpan.Zero, processed, workerCts.Token);

        // Act
        await AsteriskRealtimeIngestionDrainer.DrainAsync(
            worker,
            channel.Reader,
            workerCts,
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromSeconds(10),
            "provider",
            NullLogger.Instance,
            CancellationToken.None);

        // Assert
        Assert.True(worker.IsCompletedSuccessfully);
        Assert.False(workerCts.IsCancellationRequested);
        Assert.Equal(3, processed[0]);
    }

    [Fact]
    public async Task DrainAsync_WhenTheDrainKeepsMakingProgress_WaitsForTheFullDrain()
    {
        // Arrange — draining takes longer than one progress window, but the buffer keeps shrinking.
        var channel = CreateChannel(20);
        channel.Writer.Complete();

        using var workerCts = new CancellationTokenSource();
        var processed = new int[1];
        var worker = StartReadingWorker(channel, TimeSpan.FromMilliseconds(20), processed, workerCts.Token);

        // Act
        await AsteriskRealtimeIngestionDrainer.DrainAsync(
            worker,
            channel.Reader,
            workerCts,
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromSeconds(10),
            "provider",
            NullLogger.Instance,
            CancellationToken.None);

        // Assert — a healthy but slow drain is not truncated.
        Assert.True(worker.IsCompletedSuccessfully);
        Assert.False(workerCts.IsCancellationRequested);
        Assert.Equal(20, processed[0]);
    }

    [Fact]
    public async Task DrainAsync_WhenTheDrainStalls_AbandonsItAndCancelsTheWorker()
    {
        // Arrange — the worker never dequeues, so the buffer stays full and makes no progress.
        var channel = CreateChannel(2);
        channel.Writer.Complete();

        using var workerCts = new CancellationTokenSource();
        var worker = StartStalledWorker(workerCts.Token);

        // Act
        await AsteriskRealtimeIngestionDrainer.DrainAsync(
            worker,
            channel.Reader,
            workerCts,
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(200),
            "provider",
            NullLogger.Instance,
            CancellationToken.None);

        // Assert — a stalled dispatch is abandoned so the listener can reconnect.
        Assert.True(workerCts.IsCancellationRequested);
        Assert.True(worker.IsCompleted);
    }

    [Fact]
    public async Task DrainAsync_WhenTheListenerIsShuttingDown_ObservesTheWorkerAndReturns()
    {
        // Arrange
        var channel = CreateChannel(2);
        channel.Writer.Complete();

        using var shutdownCts = new CancellationTokenSource();
        using var workerCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownCts.Token);
        var worker = StartStalledWorker(workerCts.Token);

        await shutdownCts.CancelAsync();

        // Act
        await AsteriskRealtimeIngestionDrainer.DrainAsync(
            worker,
            channel.Reader,
            workerCts,
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(200),
            "provider",
            NullLogger.Instance,
            shutdownCts.Token);

        // Assert — shutdown returns without leaking the worker.
        Assert.True(worker.IsCompleted);
    }

    [Fact]
    public async Task DrainAsync_WhenProgressContinuesPastTheBudget_AbandonsItOnBudgetExhaustion()
    {
        // Arrange — the buffer keeps shrinking every window, but the full drain outlasts the overall budget.
        var channel = CreateChannel(50);
        channel.Writer.Complete();

        using var workerCts = new CancellationTokenSource();
        var processed = new int[1];
        var worker = StartReadingWorker(channel, TimeSpan.FromMilliseconds(30), processed, workerCts.Token);

        // Act
        await AsteriskRealtimeIngestionDrainer.DrainAsync(
            worker,
            channel.Reader,
            workerCts,
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(300),
            "provider",
            NullLogger.Instance,
            CancellationToken.None);

        // Assert — a still-progressing drain that outruns the budget is abandoned rather than allowed to run forever.
        Assert.True(workerCts.IsCancellationRequested);
        Assert.True(worker.IsCompleted);
        Assert.True(processed[0] < 50);
    }

    private static Channel<string> CreateChannel(int itemCount)
    {
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(Math.Max(itemCount, 1))
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

        for (var i = 0; i < itemCount; i++)
        {
            Assert.True(channel.Writer.TryWrite($"event-{i}"));
        }

        return channel;
    }

    private static Task StartReadingWorker(
        Channel<string> channel,
        TimeSpan perItemDelay,
        int[] processed,
        CancellationToken cancellationToken)
        => Task.Run(async () =>
        {
            await foreach (var _ in channel.Reader.ReadAllAsync(cancellationToken))
            {
                if (perItemDelay > TimeSpan.Zero)
                {
                    await Task.Delay(perItemDelay, cancellationToken);
                }

                Interlocked.Increment(ref processed[0]);
            }
        }, cancellationToken);

    private static Task StartStalledWorker(CancellationToken cancellationToken)
        => Task.Run(() => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken), cancellationToken);
}
