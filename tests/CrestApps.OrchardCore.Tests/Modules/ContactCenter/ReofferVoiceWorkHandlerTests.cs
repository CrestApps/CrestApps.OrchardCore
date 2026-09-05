using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
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
        var services = new ServiceCollection()
            .AddSingleton(inboundVoiceService.Object)
            .BuildServiceProvider();
        var handler = new ReofferVoiceWorkHandler(services);
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
    public async Task HandleAsync_WhenEventIsUnrelated_DoesNotOfferWork()
    {
        // Arrange
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        var services = new ServiceCollection()
            .AddSingleton(inboundVoiceService.Object)
            .BuildServiceProvider();
        var handler = new ReofferVoiceWorkHandler(services);

        // Act
        await handler.HandleAsync(
            new InteractionEvent { EventType = ContactCenterConstants.Events.OfferAccepted },
            TestContext.Current.CancellationToken);

        // Assert
        inboundVoiceService.Verify(
            service => service.OfferNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
