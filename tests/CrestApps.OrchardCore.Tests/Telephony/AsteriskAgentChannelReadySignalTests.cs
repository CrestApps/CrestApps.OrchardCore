using CrestApps.OrchardCore.Asterisk.Services;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskAgentChannelReadySignalTests
{
    [Fact]
    public async Task WaitAsync_WhenChannelIsSignaled_ReturnsReady()
    {
        // Arrange
        var signal = new AsteriskAgentChannelReadySignal();
        using var registration = signal.Register("agent-chan-1");

        // Act
        signal.Signal("agent-chan-1");
        var outcome = await registration.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AsteriskAgentChannelReadyOutcome.Ready, outcome);
    }

    [Fact]
    public async Task WaitAsync_WhenTimeoutElapses_ReturnsNotReady()
    {
        // Arrange
        var signal = new AsteriskAgentChannelReadySignal();
        using var registration = signal.Register("agent-chan-1");

        // Act
        var outcome = await registration.WaitAsync(TimeSpan.Zero, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AsteriskAgentChannelReadyOutcome.NotReady, outcome);
    }

    [Fact]
    public async Task WaitAsync_WhenCancellationRequested_ReturnsCanceled()
    {
        // Arrange
        var signal = new AsteriskAgentChannelReadySignal();
        using var registration = signal.Register("agent-chan-1");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var outcome = await registration.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);

        // Assert
        Assert.Equal(AsteriskAgentChannelReadyOutcome.Canceled, outcome);
    }

    [Fact]
    public async Task Signal_WhenChannelNotRegistered_DoesNotAffectOtherWaiters()
    {
        // Arrange
        var signal = new AsteriskAgentChannelReadySignal();
        using var registration = signal.Register("agent-chan-1");

        // Act
        signal.Signal("unrelated-chan");
        var outcome = await registration.WaitAsync(TimeSpan.Zero, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AsteriskAgentChannelReadyOutcome.NotReady, outcome);
    }

    [Fact]
    public async Task Signal_AfterRegistrationDisposed_DoesNotReviveWaiter()
    {
        // Arrange
        var signal = new AsteriskAgentChannelReadySignal();
        var registration = signal.Register("agent-chan-1");
        registration.Dispose();

        // Act
        signal.Signal("agent-chan-1");
        var outcome = await registration.WaitAsync(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AsteriskAgentChannelReadyOutcome.NotReady, outcome);
    }

    [Fact]
    public async Task Register_WhenSupersedingStaleRegistration_ReleasesPreviousWaiterAsNotReady()
    {
        // Arrange
        var signal = new AsteriskAgentChannelReadySignal();
        using var stale = signal.Register("agent-chan-1");

        // Act
        using var current = signal.Register("agent-chan-1");
        var staleOutcome = await stale.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        signal.Signal("agent-chan-1");
        var currentOutcome = await current.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AsteriskAgentChannelReadyOutcome.NotReady, staleOutcome);
        Assert.Equal(AsteriskAgentChannelReadyOutcome.Ready, currentOutcome);
    }
}
