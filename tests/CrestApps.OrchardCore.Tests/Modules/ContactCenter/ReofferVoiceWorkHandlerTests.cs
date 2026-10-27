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
    public async Task HandleAsync_WhenTheReleaseRequeuedAnOfferThatRangOut_OffersNextQueuedWork()
    {
        // Arrange
        // The release publishes the requeue with the offer's own payload, not the decline payload.
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        var handler = new ReofferVoiceWorkHandler(new Lazy<IInboundVoiceService>(inboundVoiceService.Object));
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.OfferRequeued,
        };

        interactionEvent.SetData(new OfferLifecycleEventData
        {
            ReservationId = "r1",
            QueueId = "q1",
        });

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        inboundVoiceService.Verify(
            service => service.OfferNextAsync("q1", TestContext.Current.CancellationToken),
            Times.Once);
    }

    [Theory]
    [InlineData(ContactCenterConstants.Events.OfferAccepted)]
    [InlineData(ContactCenterConstants.Events.OfferExpired)]
    public async Task HandleAsync_WhenEventIsUnrelated_DoesNotOfferWork(string eventType)
    {
        // Arrange
        // An offer that rang out is re-offered through the requeue its release publishes, which knows what became of
        // the caller; the expiry's reason names the queue's configured action, which a fallback can override.
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        var handler = new ReofferVoiceWorkHandler(new Lazy<IInboundVoiceService>(inboundVoiceService.Object));
        var interactionEvent = new InteractionEvent
        {
            EventType = eventType,
        };

        interactionEvent.SetData(new OfferLifecycleEventData
        {
            QueueId = "q1",
            Reason = nameof(UnansweredOfferAction.Requeue),
        });

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        inboundVoiceService.Verify(
            service => service.OfferNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
