using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// A realtime AI conversation lives in the process, so a restart ended it and left the caller on a silent line until
/// they hung up. When the host begins to stop, every live conversation is now told to end, and the node waits,
/// briefly, for each caller to be handed on.
/// </summary>
public sealed class AIVoiceCallShutdownTests
{
    [Fact]
    public async Task Stopping_TellsLiveConversationsToEnd()
    {
        // Arrange
        var lifetime = new StubHostApplicationLifetime();
        using var shutdown = CreateShutdown(lifetime);
        await shutdown.ActivatedAsync();
        var call = shutdown.HoldCall();

        // Act
        var stopping = Task.Run(lifetime.StopApplication, TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => shutdown.Stopping.IsCancellationRequested);
        call.Dispose();
        await stopping.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(shutdown.Stopping.IsCancellationRequested);
    }

    [Fact]
    public async Task Stopping_StillFires_WhenNoCallIsLive()
    {
        // Arrange
        // A call answered while the node is going down must be handed on the moment its conversation starts.
        var lifetime = new StubHostApplicationLifetime();
        using var shutdown = CreateShutdown(lifetime);
        await shutdown.ActivatedAsync();

        // Act
        lifetime.StopApplication();

        // Assert
        Assert.True(shutdown.Stopping.IsCancellationRequested);
    }

    [Fact]
    public async Task Stopping_WaitsUntilTheLiveCallHasBeenReleased()
    {
        // Arrange
        var lifetime = new StubHostApplicationLifetime();
        using var shutdown = CreateShutdown(lifetime);
        await shutdown.ActivatedAsync();
        var call = shutdown.HoldCall();

        // Act
        var stopping = Task.Run(lifetime.StopApplication, TestContext.Current.CancellationToken);
        var finishedEarly = await Task.WhenAny(stopping, Task.Delay(200, TestContext.Current.CancellationToken)) == stopping;
        call.Dispose();

        // Assert
        Assert.False(finishedEarly);
        await stopping.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(0, shutdown.LiveCalls);
    }

    [Fact]
    public async Task Stopping_WaitsForTheWorkThatHandsTheCallerOn()
    {
        // Arrange
        var lifetime = new StubHostApplicationLifetime();
        using var shutdown = CreateShutdown(lifetime);
        await shutdown.ActivatedAsync();
        var call = shutdown.HoldCall();
        var handOn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        shutdown.TrackHandOn(handOn.Task);
        call.Dispose();

        // Act
        var stopping = Task.Run(lifetime.StopApplication, TestContext.Current.CancellationToken);
        var finishedEarly = await Task.WhenAny(stopping, Task.Delay(200, TestContext.Current.CancellationToken)) == stopping;
        handOn.SetResult();

        // Assert
        Assert.False(finishedEarly);
        await stopping.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Stopping_GivesUpWaiting_OnceTheTimeoutPasses()
    {
        // Arrange
        var lifetime = new StubHostApplicationLifetime();
        var time = new ManualTimeProvider();
        using var shutdown = new AIVoiceCallShutdown(lifetime, time, NullLogger.Instance);
        await shutdown.ActivatedAsync();
        using var stuck = shutdown.HoldCall();

        // Act
        var stopping = Task.Run(lifetime.StopApplication, TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => shutdown.Stopping.IsCancellationRequested);
        time.Advance(AIVoiceCallShutdown.HandOnTimeout + TimeSpan.FromSeconds(1));

        // Assert
        await stopping.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(1, shutdown.LiveCalls);
    }

    [Fact]
    public async Task ActivatedAsync_FiresAtOnce_WhenTheHostIsAlreadyStopping()
    {
        // Arrange
        var lifetime = new StubHostApplicationLifetime();
        lifetime.StopApplication();
        using var shutdown = CreateShutdown(lifetime);

        // Act
        await shutdown.ActivatedAsync();

        // Assert
        Assert.True(shutdown.Stopping.IsCancellationRequested);
    }

    [Fact]
    public async Task Stopping_DoesNothing_AfterTheShellIsDisposed()
    {
        // Arrange
        // A reloaded tenant disposes the old shell; its registration must not act on what is left of it.
        var lifetime = new StubHostApplicationLifetime();
        var shutdown = CreateShutdown(lifetime);
        await shutdown.ActivatedAsync();
        shutdown.Dispose();

        // Act
        var exception = Record.Exception(lifetime.StopApplication);

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task Stopping_DoesNotWaitOnHandOnWorkThatHasAlreadyFinished()
    {
        // Arrange
        var lifetime = new StubHostApplicationLifetime();
        var time = new ManualTimeProvider();
        using var shutdown = new AIVoiceCallShutdown(lifetime, time, NullLogger.Instance);
        await shutdown.ActivatedAsync();
        var call = shutdown.HoldCall();
        var handOn = new TaskCompletionSource();
        shutdown.TrackHandOn(handOn.Task);
        handOn.SetResult();
        call.Dispose();

        // Act
        // The clock never moves, so a stop that waited on the finished work would never return.
        var stopping = Task.Run(lifetime.StopApplication, TestContext.Current.CancellationToken);

        // Assert
        await stopping.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    private static AIVoiceCallShutdown CreateShutdown(IHostApplicationLifetime lifetime)
        => new(lifetime, NullLogger<AIVoiceCallShutdown>.Instance);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition was never met.");

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private sealed class StubHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _stopping = new();

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => _stopping.Cancel();
    }

    /// <summary>
    /// A clock that only moves when the test moves it.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _timestamp);

        public void Advance(TimeSpan by) => Interlocked.Add(ref _timestamp, by.Ticks);
    }
}
