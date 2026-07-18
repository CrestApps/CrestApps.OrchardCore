using CrestApps.OrchardCore.Asterisk.Services;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskAgentChannelReadySignalTests
{
    [Fact]
    public async Task WaitAsync_WhenChannelIsSignaled_ReturnsTrue()
    {
        // Arrange
        var signal = new AsteriskAgentChannelReadySignal();
        using var registration = signal.Register("agent-chan-1");

        // Act
        signal.Signal("agent-chan-1");
        var ready = await registration.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(ready);
    }

    [Fact]
    public async Task WaitAsync_WhenTimeoutElapses_ReturnsFalse()
    {
        // Arrange
        var signal = new AsteriskAgentChannelReadySignal();
        using var registration = signal.Register("agent-chan-1");

        // Act
        var ready = await registration.WaitAsync(TimeSpan.Zero, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(ready);
    }

    [Fact]
    public async Task Signal_WhenChannelNotRegistered_DoesNotAffectOtherWaiters()
    {
        // Arrange
        var signal = new AsteriskAgentChannelReadySignal();
        using var registration = signal.Register("agent-chan-1");

        // Act
        signal.Signal("unrelated-chan");
        var ready = await registration.WaitAsync(TimeSpan.Zero, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(ready);
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
        var ready = await registration.WaitAsync(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(ready);
    }

    [Fact]
    public async Task Register_WhenSupersedingStaleRegistration_ReleasesPreviousWaiterAsNotReady()
    {
        // Arrange
        var signal = new AsteriskAgentChannelReadySignal();
        using var stale = signal.Register("agent-chan-1");

        // Act
        using var current = signal.Register("agent-chan-1");
        var staleReady = await stale.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        signal.Signal("agent-chan-1");
        var currentReady = await current.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(staleReady);
        Assert.True(currentReady);
    }
}
