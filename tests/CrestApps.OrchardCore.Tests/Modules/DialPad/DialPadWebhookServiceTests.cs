using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
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
        var eventService = new Mock<IProviderVoiceEventService>();
        eventService.Setup(s => s.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync((CallSession)null);

        var router = new Mock<IVoiceContactCenterCallRouter>();
        router.Setup(r => r.RouteInboundAsync(It.IsAny<InboundVoiceEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(new InboundVoiceRoutingResult());

        var service = CreateService(eventService, router);

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
        router.Verify(r => r.RouteInboundAsync(It.Is<InboundVoiceEvent>(e => e.ProviderCallId == "c1" && e.FromAddress == "+15551112222"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ExistingInteraction_UpdatesWithoutRouting()
    {
        // Arrange
        var eventService = new Mock<IProviderVoiceEventService>();
        eventService.Setup(s => s.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CallSession { ItemId = "cs1" });

        var router = new Mock<IVoiceContactCenterCallRouter>();
        var service = CreateService(eventService, router);

        var callEvent = new DialPadCallEvent { CallId = "c1", State = "connected", Direction = "inbound" };

        // Act
        var result = await service.ProcessAsync(callEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DialPadWebhookResult.Updated, result);
        router.Verify(r => r.RouteInboundAsync(It.IsAny<InboundVoiceEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_OutboundWithNoInteraction_Ignored()
    {
        // Arrange
        var eventService = new Mock<IProviderVoiceEventService>();
        eventService.Setup(s => s.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync((CallSession)null);

        var router = new Mock<IVoiceContactCenterCallRouter>();
        var service = CreateService(eventService, router);

        var callEvent = new DialPadCallEvent { CallId = "c1", State = "connected", Direction = "outbound" };

        // Act
        var result = await service.ProcessAsync(callEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DialPadWebhookResult.Ignored, result);
        router.Verify(r => r.RouteInboundAsync(It.IsAny<InboundVoiceEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_UnknownState_IgnoredWithoutIngest()
    {
        // Arrange
        var eventService = new Mock<IProviderVoiceEventService>();
        var router = new Mock<IVoiceContactCenterCallRouter>();
        var service = CreateService(eventService, router);

        var callEvent = new DialPadCallEvent { CallId = "c1", State = "something_odd", Direction = "inbound" };

        // Act
        var result = await service.ProcessAsync(callEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DialPadWebhookResult.Ignored, result);
        eventService.Verify(s => s.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithRicherProviderDetails_PassesNormalizedDetailsToEventService()
    {
        // Arrange
        ProviderVoiceEvent providerEvent = null;
        var eventService = new Mock<IProviderVoiceEventService>();
        eventService.Setup(s => s.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderVoiceEvent, CancellationToken>((value, _) => providerEvent = value)
            .ReturnsAsync(new CallSession { ItemId = "cs1" });

        var router = new Mock<IVoiceContactCenterCallRouter>();
        var service = CreateService(eventService, router);

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

    private static DialPadWebhookService CreateService(Mock<IProviderVoiceEventService> eventService, Mock<IVoiceContactCenterCallRouter> router)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new DialPadWebhookService(eventService.Object, router.Object, clock.Object);
    }
}
