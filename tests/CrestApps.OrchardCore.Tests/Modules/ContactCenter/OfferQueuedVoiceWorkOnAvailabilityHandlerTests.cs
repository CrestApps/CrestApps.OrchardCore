using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class OfferQueuedVoiceWorkOnAvailabilityHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenAgentSignsIn_OffersQueuedVoiceWorkForAgent()
    {
        // Arrange
        var queuedVoiceWorkOfferService = new Mock<IQueuedVoiceWorkOfferService>();
        var handler = new OfferQueuedVoiceWorkOnAvailabilityHandler(CreateServices(queuedVoiceWorkOfferService.Object));

        // Act
        await handler.HandleAsync(new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.AgentSignedIn,
            AggregateId = "a1",
        }, TestContext.Current.CancellationToken);

        // Assert
        queuedVoiceWorkOfferService.Verify(service => service.OfferForAgentAsync("a1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenEventTypeDoesNotMatch_DoesNotOfferQueuedVoiceWork()
    {
        // Arrange
        var queuedVoiceWorkOfferService = new Mock<IQueuedVoiceWorkOfferService>();
        var handler = new OfferQueuedVoiceWorkOnAvailabilityHandler(CreateServices(queuedVoiceWorkOfferService.Object));

        // Act
        await handler.HandleAsync(new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.QueueItemAdded,
            AggregateId = "a1",
        }, TestContext.Current.CancellationToken);

        // Assert
        queuedVoiceWorkOfferService.Verify(service => service.OfferForAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenQueuedVoiceOfferServiceIsMissing_ReturnsWithoutFailure()
    {
        // Arrange
        var handler = new OfferQueuedVoiceWorkOnAvailabilityHandler(new ServiceCollection().BuildServiceProvider());

        // Act
        await handler.HandleAsync(new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.AgentSignedIn,
            AggregateId = "a1",
        }, TestContext.Current.CancellationToken);

        // Assert
    }

    private static ServiceProvider CreateServices(IQueuedVoiceWorkOfferService queuedVoiceWorkOfferService)
    {
        return new ServiceCollection()
            .AddSingleton(queuedVoiceWorkOfferService)
            .AddSingleton<IQueuedVoiceWorkOfferService>(queuedVoiceWorkOfferService)
            .BuildServiceProvider();
    }
}
