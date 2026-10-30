using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Tests.Modules.ContactCenter;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// The provider's answering-machine reports, carried from the webhook request that receives them to the live
/// session holding the call in another.
/// </summary>
public sealed class AnsweringMachineSignalsTests
{
    private static readonly DateTime _now = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MachineDetected_ReportedBeforeAnybodyWatches_IsSeenByTheWatch()
    {
        // Arrange
        // Webhooks are not ordered: a quick detection can be processed before the answered event that starts the
        // session watching for it.
        var signals = new AnsweringMachineSignals(new AdvanceableClock(_now));
        signals.MachineDetected("Fake", "call-1");

        // Act
        var signal = signals.Watch("Fake", "call-1");

        // Assert
        Assert.True(signal.MachineDetected.IsCancellationRequested);
        Assert.False(signal.GreetingEnded.IsCancellationRequested);
    }

    [Fact]
    public void MachineDetected_ReportedWhileWatching_FiresTheWatchedToken()
    {
        // Arrange
        var signals = new AnsweringMachineSignals(new AdvanceableClock(_now));
        var signal = signals.Watch("Fake", "call-1");
        var fired = false;
        using var registration = signal.MachineDetected.Register(() => fired = true);

        // Act
        signals.MachineDetected("Fake", "call-1");

        // Assert
        Assert.True(fired);
        Assert.True(signal.MachineDetected.IsCancellationRequested);
    }

    [Fact]
    public void GreetingEnded_IsReportedOnItsOwnToken()
    {
        // Arrange
        var signals = new AnsweringMachineSignals(new AdvanceableClock(_now));
        var signal = signals.Watch("Fake", "call-1");

        // Act
        signals.GreetingEnded("Fake", "call-1");

        // Assert
        Assert.True(signal.GreetingEnded.IsCancellationRequested);
        Assert.False(signal.MachineDetected.IsCancellationRequested);
    }

    [Fact]
    public void PhoneMenuReached_IsReportedOnItsOwnToken()
    {
        // Arrange
        // Live, the assistant pressed the key for the person it was calling, the menu was taken for a voicemail, and
        // a message was left and the call hung up on. Pressing keys has to reach the session watching the call.
        var signals = new AnsweringMachineSignals(new AdvanceableClock(_now));
        signals.MachineDetected("Fake", "call-1");
        var signal = signals.Watch("Fake", "call-1");

        // Act
        signals.PhoneMenuReached("Fake", "call-1");

        // Assert
        Assert.True(signal.PhoneMenuReached.IsCancellationRequested);
        Assert.False(signal.GreetingEnded.IsCancellationRequested);
        Assert.False(signals.Watch("Fake", "call-2").PhoneMenuReached.IsCancellationRequested);
    }

    [Fact]
    public void Forget_DropsTheCall_SoALaterWatchStartsClean()
    {
        // Arrange
        var signals = new AnsweringMachineSignals(new AdvanceableClock(_now));
        signals.MachineDetected("Fake", "call-1");
        signals.GreetingEnded("Fake", "call-1");

        // Act
        signals.Forget("Fake", "call-1");

        // Assert
        Assert.Equal(0, signals.Count);
        var later = signals.Watch("Fake", "call-1");
        Assert.False(later.MachineDetected.IsCancellationRequested);
        Assert.False(later.GreetingEnded.IsCancellationRequested);
    }

    [Fact]
    public void Calls_AreKeptApart_ByProviderAndCallId()
    {
        // Arrange
        var signals = new AnsweringMachineSignals(new AdvanceableClock(_now));

        // Act
        signals.MachineDetected("Fake", "call-1");

        // Assert
        Assert.False(signals.Watch("Fake", "call-2").MachineDetected.IsCancellationRequested);
        Assert.False(signals.Watch("OtherFake", "call-1").MachineDetected.IsCancellationRequested);
        Assert.True(signals.Watch("Fake", "call-1").MachineDetected.IsCancellationRequested);
    }

    [Fact]
    public void ACallNobodyForgot_IsDroppedOnceOlderThanAnyCall()
    {
        // Arrange
        // A hangup the provider never reported leaves a call nobody forgets; it must not stay for good.
        var clock = new AdvanceableClock(_now);
        var signals = new AnsweringMachineSignals(clock);
        signals.MachineDetected("Fake", "call-1");

        // Act
        clock.Advance(AnsweringMachineSignals.Retention + TimeSpan.FromMinutes(1));
        var current = signals.Watch("Fake", "call-2");

        // Assert
        Assert.Equal(1, signals.Count);
        Assert.False(current.MachineDetected.IsCancellationRequested);
        Assert.False(signals.Watch("Fake", "call-1").MachineDetected.IsCancellationRequested);
    }

    [Theory]
    [InlineData(null, "call-1")]
    [InlineData("Fake", "")]
    public void Watch_WithoutACall_IsRefused(string providerName, string providerCallId)
    {
        // Arrange
        var signals = new AnsweringMachineSignals(new AdvanceableClock(_now));

        // Act
        var exception = Record.Exception(() => signals.Watch(providerName, providerCallId));

        // Assert
        Assert.IsAssignableFrom<ArgumentException>(exception);
    }
}
