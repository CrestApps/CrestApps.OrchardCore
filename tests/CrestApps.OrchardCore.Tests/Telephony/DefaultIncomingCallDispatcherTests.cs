using CrestApps.OrchardCore.Telephony;
using CrestApps.Core.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using OrchardCore.Modules;
using CrestApps.Core.Telephony;
using CrestApps.Core.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class DefaultIncomingCallDispatcherTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 9, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DispatchAsync_CreatesInboundInteraction_AndPushesIncomingCall()
    {
        // Arrange
        var notifier = new Mock<ITelephonySoftPhoneNotifier>();
        var store = new Mock<ITelephonyInteractionStore>();
        var clock = new FakeTimeProvider();
        var logger = new Mock<ILogger<DefaultIncomingCallDispatcher>>();

        clock.SetUtcNow(_now.UtcDateTime);
        store.Setup(value => value.FindByCallIdAsync("user-1", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelephonyInteraction)null);

        TelephonyInteraction createdInteraction = null;
        store.Setup(value => value.CreateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()))
            .Callback<TelephonyInteraction, CancellationToken>((interaction, _) => createdInteraction = interaction)
            .Returns(() => Task.CompletedTask);

        var dispatcher = new DefaultIncomingCallDispatcher(
            notifier.Object,
            [],
            store.Object,
            clock,
            logger.Object);
        var call = new TelephonyCall
        {
            CallId = "call-1",
            ProviderName = "Asterisk",
            From = "+15550001000",
            To = "+15550002000",
            Direction = CallDirection.Inbound,
            StartedUtc = _now,
        };

        // Act
        await dispatcher.DispatchAsync("user-1", call, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(createdInteraction);
        Assert.Equal("call-1", createdInteraction.CallId);
        Assert.Equal("+15550001000", createdInteraction.From);
        Assert.Equal(CallDirection.Inbound, createdInteraction.Direction);
        Assert.Equal(CallOutcome.InProgress, createdInteraction.Outcome);

        notifier.Verify(
            value => value.NotifyIncomingCallAsync(
                "user-1",
                It.Is<TelephonyCall>(incoming => incoming.CallId == "call-1"),
                It.IsAny<IncomingCallContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_RefreshesExistingInboundInteraction()
    {
        // Arrange
        var notifier = new Mock<ITelephonySoftPhoneNotifier>();
        var store = new Mock<ITelephonyInteractionStore>();
        var clock = new FakeTimeProvider();
        var logger = new Mock<ILogger<DefaultIncomingCallDispatcher>>();
        var existing = new TelephonyInteraction
        {
            InteractionId = "int-1",
            CallId = "call-1",
            From = "+15551110000",
            To = "+15552220000",
            Direction = CallDirection.Outbound,
            Outcome = CallOutcome.InProgress,
            StartedUtc = _now.UtcDateTime.AddMinutes(-1),
        };

        clock.SetUtcNow(_now.UtcDateTime);
        store.Setup(value => value.FindByCallIdAsync("user-1", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        store.SetupRetryingUpdates(existing);

        var dispatcher = new DefaultIncomingCallDispatcher(
            notifier.Object,
            [],
            store.Object,
            clock,
            logger.Object);
        var call = new TelephonyCall
        {
            CallId = "call-1",
            ProviderName = "Asterisk",
            From = "+15550001000",
            To = "+15550002000",
            Direction = CallDirection.Inbound,
        };

        // Act
        await dispatcher.DispatchAsync("user-1", call, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("+15550001000", existing.From);
        Assert.Equal("+15550002000", existing.To);
        Assert.Equal(CallDirection.Inbound, existing.Direction);

        store.Verify(
            value => value.UpdateByIdAsync(
                "int-1",
                It.IsAny<Func<TelephonyInteraction, bool>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        store.Verify(value => value.CreateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
