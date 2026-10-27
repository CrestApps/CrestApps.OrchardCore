using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ReofferVoiceWorkHandlerTests
{
    [Theory]
    [InlineData(ContactCenterConstants.Events.OfferDeclined)]
    [InlineData(ContactCenterConstants.Events.OfferRequeued)]
    public async Task HandleAsync_WhenWorkRequiresReoffer_OffersNextQueuedWork(string eventType)
    {
        // Arrange
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        var handler = new ReofferVoiceWorkHandler(new Lazy<IInboundVoiceService>(inboundVoiceService.Object));
        var interactionEvent = new InteractionEvent
        {
            EventType = eventType,
        };

        interactionEvent.SetData(new OfferDeclinedEventData
        {
            QueueId = "q1",
        });

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        inboundVoiceService.Verify(
            service => service.OfferNextAsync("q1", TestContext.Current.CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenOfferRangOutBackIntoItsQueue_OffersNextQueuedWork()
    {
        // Arrange
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        var handler = new ReofferVoiceWorkHandler(new Lazy<IInboundVoiceService>(inboundVoiceService.Object));

        // Act
        await handler.HandleAsync(CreateExpiredOffer("q1", nameof(UnansweredOfferAction.Requeue)), TestContext.Current.CancellationToken);

        // Assert
        inboundVoiceService.Verify(
            service => service.OfferNextAsync("q1", TestContext.Current.CancellationToken),
            Times.Once);
    }

    [Theory]
    [InlineData("q1", nameof(UnansweredOfferAction.Voicemail))]
    [InlineData("q1", nameof(UnansweredOfferAction.Reject))]
    [InlineData(ContactCenterConstants.DirectRouting.QueueId, nameof(UnansweredOfferAction.Requeue))]
    [InlineData(ContactCenterConstants.CampaignQueue.Prefix + "c1", nameof(UnansweredOfferAction.Requeue))]
    public async Task HandleAsync_WhenOfferRangOutButNoNextAgentIsDue_DoesNotOfferWork(string queueId, string reason)
    {
        // Arrange
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        var handler = new ReofferVoiceWorkHandler(new Lazy<IInboundVoiceService>(inboundVoiceService.Object));

        // Act
        await handler.HandleAsync(CreateExpiredOffer(queueId, reason), TestContext.Current.CancellationToken);

        // Assert
        inboundVoiceService.Verify(
            service => service.OfferNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenEventIsUnrelated_DoesNotOfferWork()
    {
        // Arrange
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        var handler = new ReofferVoiceWorkHandler(new Lazy<IInboundVoiceService>(inboundVoiceService.Object));

        // Act
        await handler.HandleAsync(
            new InteractionEvent { EventType = ContactCenterConstants.Events.OfferAccepted },
            TestContext.Current.CancellationToken);

        // Assert
        inboundVoiceService.Verify(
            service => service.OfferNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static InteractionEvent CreateExpiredOffer(string queueId, string reason)
    {
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.OfferExpired,
        };

        interactionEvent.SetData(new OfferLifecycleEventData
        {
            QueueId = queueId,
            Reason = reason,
        });

        return interactionEvent;
    }
}
