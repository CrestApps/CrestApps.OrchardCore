using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.SignalR;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskRealtimeVoiceEventDispatcherTests
{
    private static readonly ShellSettings _shellSettings = new()
    {
        Name = "TenantA",
    };

    [Fact]
    public async Task HandleAsync_WhenPlainTelephonyInteractionMatches_EndsInteraction_AndPushesDisconnectedState()
    {
        // Arrange
        var store = new Mock<ITelephonyInteractionStore>();
        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        var clients = new Mock<IHubClients<ITelephonyClient>>();
        var client = new Mock<ITelephonyClient>();
        var clock = new Mock<IClock>();
        var startedUtc = new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc);
        var endedUtc = new DateTime(2026, 7, 10, 15, 3, 0, DateTimeKind.Utc);
        var interaction = new TelephonyInteraction
        {
            InteractionId = "interaction-1",
            CallId = "call-1",
            ProviderName = "Asterisk",
            UserId = "user-1",
            UserName = "mike",
            From = "+15550001000",
            To = "+15550002000",
            Direction = CallDirection.Outbound,
            Outcome = CallOutcome.InProgress,
            StartedUtc = startedUtc,
        };

        store
            .Setup(value => value.FindByProviderCallIdAsync("Asterisk", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);
        clients.Setup(value => value.Group(TenantSignalRGroupName.ForUser(_shellSettings.Name, "user-1"))).Returns(client.Object);
        clock.SetupGet(value => value.UtcNow).Returns(endedUtc);

        var dispatcher = new AsteriskRealtimeVoiceEventDispatcher(
            [],
            [],
            store.Object,
            hubContext.Object,
            clock.Object,
            NullLogger<AsteriskRealtimeVoiceEventDispatcher>.Instance,
            _shellSettings);
        var voiceEvent = new AsteriskRealtimeVoiceEvent
        {
            ProviderName = "Asterisk",
            CallId = "call-1",
            EventType = "ChannelDestroyed",
            State = CallState.Disconnected,
            FromAddress = "+15550001000",
            ToAddress = "+15550002000",
            OccurredUtc = endedUtc,
        };

        // Act
        await dispatcher.HandleAsync(voiceEvent, TestContext.Current.CancellationToken);
        voiceEvent.EventType = "ChannelDestroyed";
        await dispatcher.HandleAsync(voiceEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CallOutcome.Completed, interaction.Outcome);
        Assert.Equal(endedUtc, interaction.EndedUtc);
        Assert.Equal(180, interaction.DurationSeconds);

        store.Verify(value => value.UpdateAsync(interaction, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(
            value => value.CallStateChanged(It.Is<TelephonyCall>(call =>
                call.CallId == "call-1" &&
                call.State == CallState.Disconnected &&
                call.Direction == CallDirection.Outbound)),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenContactCenterOwnsCall_SkipsDirectSoftPhoneProjection()
    {
        // Arrange
        var store = new Mock<ITelephonyInteractionStore>();
        var providerVoiceEventSink = new Mock<IProviderVoiceEventSink>();
        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        var clock = new Mock<IClock>();
        providerVoiceEventSink
            .Setup(service => service.IngestAsync(It.Is<ProviderVoiceEvent>(value =>
                    value.ProviderCallId == "call-1" &&
                    value.State == ContactCenterCallState.Ended),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var voiceEventBridge = new AsteriskContactCenterVoiceEventBridge(
            providerVoiceEventSink.Object,
            NullLogger<AsteriskContactCenterVoiceEventBridge>.Instance);

        var dispatcher = new AsteriskRealtimeVoiceEventDispatcher(
            [voiceEventBridge],
            [],
            store.Object,
            hubContext.Object,
            clock.Object,
            NullLogger<AsteriskRealtimeVoiceEventDispatcher>.Instance,
            _shellSettings);
        var voiceEvent = new AsteriskRealtimeVoiceEvent
        {
            ProviderName = "Asterisk",
            CallId = "call-1",
            EventType = "ChannelDestroyed",
            State = CallState.Disconnected,
        };

        // Act
        await dispatcher.HandleAsync(voiceEvent, TestContext.Current.CancellationToken);

        // Assert
        store.Verify(value => value.FindByProviderCallIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        hubContext.VerifyGet(value => value.Clients, Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenBridgeThrows_StillRunsTerminalTeardown()
    {
        // Arrange
        var store = new Mock<ITelephonyInteractionStore>();
        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        var clock = new Mock<IClock>();

        var throwingBridge = new Mock<IAsteriskRealtimeVoiceEventBridge>();
        throwingBridge
            .Setup(bridge => bridge.TryHandleAsync(It.IsAny<AsteriskRealtimeVoiceEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bridge failure"));

        var teardownService = new Mock<IAsteriskCallTeardownService>();

        var dispatcher = new AsteriskRealtimeVoiceEventDispatcher(
            [throwingBridge.Object],
            [teardownService.Object],
            store.Object,
            hubContext.Object,
            clock.Object,
            NullLogger<AsteriskRealtimeVoiceEventDispatcher>.Instance,
            _shellSettings);
        var voiceEvent = new AsteriskRealtimeVoiceEvent
        {
            ProviderName = "Asterisk",
            CallId = "call-1",
            EventType = "ChannelDestroyed",
            State = CallState.Disconnected,
        };

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.HandleAsync(voiceEvent, TestContext.Current.CancellationToken));

        // Assert
        teardownService.Verify(
            service => service.ReleaseAsync(voiceEvent, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
