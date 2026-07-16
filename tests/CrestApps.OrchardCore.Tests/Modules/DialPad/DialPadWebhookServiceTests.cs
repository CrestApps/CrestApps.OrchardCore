using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.DialPad.Services;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.DialPad;

public sealed class DialPadWebhookServiceTests
{
    private static readonly DateTime _now = new(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProcessAsync_NewInboundRingingCall_RoutesInbound()
    {
        // Arrange
        var eventSink = new Mock<IProviderVoiceEventSink>();
        eventSink.Setup(s => s.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var inboundSink = new Mock<IInboundVoiceEventSink>();

        var service = CreateService(eventSink, inboundSink);

        var callEvent = new DialPadCallEvent
        {
            CallId = "c1",
            State = "ringing",
            Direction = "inbound",
            ExternalNumber = "+15551112222",
            InternalNumber = "+15553334444",
        };

        // Act
        var result = await service.ProcessAsync(callEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DialPadWebhookResult.Routed, result);
        inboundSink.Verify(
            sink => sink.RouteAsync(
                It.Is<InboundVoiceEvent>(e => e.ProviderCallId == "c1" && e.FromAddress == "+15551112222"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ExistingInteraction_UpdatesWithoutRouting()
    {
        // Arrange
        var eventSink = new Mock<IProviderVoiceEventSink>();
        eventSink.Setup(s => s.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var inboundSink = new Mock<IInboundVoiceEventSink>();
        var service = CreateService(eventSink, inboundSink);

        var callEvent = new DialPadCallEvent { CallId = "c1", State = "connected", Direction = "inbound" };

        // Act
        var result = await service.ProcessAsync(callEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DialPadWebhookResult.Updated, result);
        inboundSink.Verify(
            sink => sink.RouteAsync(It.IsAny<InboundVoiceEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_OutboundWithNoInteraction_Ignored()
    {
        // Arrange
        var eventSink = new Mock<IProviderVoiceEventSink>();
        eventSink.Setup(s => s.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var inboundSink = new Mock<IInboundVoiceEventSink>();
        var service = CreateService(eventSink, inboundSink);

        var callEvent = new DialPadCallEvent { CallId = "c1", State = "connected", Direction = "outbound" };

        // Act
        var result = await service.ProcessAsync(callEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DialPadWebhookResult.Ignored, result);
        inboundSink.Verify(
            sink => sink.RouteAsync(It.IsAny<InboundVoiceEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_UnknownState_IgnoredWithoutIngest()
    {
        // Arrange
        var eventSink = new Mock<IProviderVoiceEventSink>();
        var inboundSink = new Mock<IInboundVoiceEventSink>();
        var service = CreateService(eventSink, inboundSink);

        var callEvent = new DialPadCallEvent { CallId = "c1", State = "something_odd", Direction = "inbound" };

        // Act
        var result = await service.ProcessAsync(callEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DialPadWebhookResult.Ignored, result);
        eventSink.Verify(s => s.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithRicherProviderDetails_PassesNormalizedDetailsToEventService()
    {
        // Arrange
        ProviderVoiceEvent providerEvent = null;
        var eventSink = new Mock<IProviderVoiceEventSink>();
        eventSink.Setup(s => s.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderVoiceEvent, CancellationToken>((value, _) => providerEvent = value)
            .ReturnsAsync(true);

        var inboundSink = new Mock<IInboundVoiceEventSink>();
        var service = CreateService(eventSink, inboundSink);

        var callEvent = new DialPadCallEvent
        {
            CallId = "c1",
            State = "connected",
            Direction = "inbound",
            IsMuted = true,
            RecordingState = "paused",
            RecordingId = "rec-1",
            IsConference = true,
            ParticipantCount = 3,
        };

        // Act
        var result = await service.ProcessAsync(callEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DialPadWebhookResult.Updated, result);
        Assert.NotNull(providerEvent);
        Assert.True(providerEvent.IsMuted);
        Assert.Equal(RecordingState.Paused, providerEvent.RecordingState);
        Assert.Equal("rec-1", providerEvent.RecordingReference);
        Assert.True(providerEvent.IsConference);
        Assert.Equal(3, providerEvent.ParticipantCount);
        Assert.Equal("connected", providerEvent.Metadata["dialPadState"]);
    }

    [Fact]
    public async Task ProcessAsync_WhenStateAttributesChange_UsesDifferentIdempotencyKeys()
    {
        // Arrange
        var providerEvents = new List<ProviderVoiceEvent>();
        var eventSink = new Mock<IProviderVoiceEventSink>();
        eventSink
            .Setup(s => s.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderVoiceEvent, CancellationToken>((value, _) => providerEvents.Add(value))
            .ReturnsAsync(true);
        var service = CreateService(eventSink, new Mock<IInboundVoiceEventSink>());

        // Act
        await service.ProcessAsync(new DialPadCallEvent
        {
            CallId = "c1",
            State = "connected",
            Direction = "inbound",
            IsMuted = false,
        }, TestContext.Current.CancellationToken);
        await service.ProcessAsync(new DialPadCallEvent
        {
            CallId = "c1",
            State = "connected",
            Direction = "inbound",
            IsMuted = true,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, providerEvents.Count);
        Assert.NotEqual(providerEvents[0].IdempotencyKey, providerEvents[1].IdempotencyKey);
    }

    [Fact]
    public async Task ProcessAsync_VoicemailState_YieldsEndedWithMachineClassification()
    {
        // Arrange
        ProviderVoiceEvent captured = null;
        var eventSink = new Mock<IProviderVoiceEventSink>();
        eventSink
            .Setup(s => s.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderVoiceEvent, CancellationToken>((value, _) => captured = value)
            .ReturnsAsync(true);

        var service = CreateService(eventSink, new Mock<IInboundVoiceEventSink>());
        var callEvent = new DialPadCallEvent { CallId = "c1", State = "voicemail", Direction = "outbound" };

        // Act
        var result = await service.ProcessAsync(callEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DialPadWebhookResult.Updated, result);
        Assert.NotNull(captured);
        Assert.Equal(ContactCenterCallState.Ended, captured.State);
        Assert.Equal(AnswerClassification.Machine, captured.AnswerClassification);
    }

    [Fact]
    public async Task ProcessAsync_FaxState_YieldsEndedWithFaxClassification()
    {
        // Arrange
        ProviderVoiceEvent captured = null;
        var eventSink = new Mock<IProviderVoiceEventSink>();
        eventSink
            .Setup(s => s.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderVoiceEvent, CancellationToken>((value, _) => captured = value)
            .ReturnsAsync(true);

        var service = CreateService(eventSink, new Mock<IInboundVoiceEventSink>());
        var callEvent = new DialPadCallEvent { CallId = "c1", State = "fax", Direction = "outbound" };

        // Act
        var result = await service.ProcessAsync(callEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DialPadWebhookResult.Updated, result);
        Assert.NotNull(captured);
        Assert.Equal(ContactCenterCallState.Ended, captured.State);
        Assert.Equal(AnswerClassification.Fax, captured.AnswerClassification);
    }

    [Fact]
    public async Task ProcessAsync_ConnectedState_YieldsNullAnswerClassification()
    {
        // Arrange
        ProviderVoiceEvent captured = null;
        var eventSink = new Mock<IProviderVoiceEventSink>();
        eventSink
            .Setup(s => s.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderVoiceEvent, CancellationToken>((value, _) => captured = value)
            .ReturnsAsync(true);

        var service = CreateService(eventSink, new Mock<IInboundVoiceEventSink>());
        var callEvent = new DialPadCallEvent { CallId = "c1", State = "connected", Direction = "outbound" };

        // Act
        await service.ProcessAsync(callEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(ContactCenterCallState.Connected, captured.State);
        Assert.Null(captured.AnswerClassification);
    }

    private static DialPadWebhookService CreateService(
        Mock<IProviderVoiceEventSink> eventSink,
        Mock<IInboundVoiceEventSink> inboundSink)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new DialPadWebhookService(eventSink.Object, inboundSink.Object, clock.Object);
    }
}
