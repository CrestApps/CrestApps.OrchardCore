using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
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
        var handler = CreateHandler(queuedVoiceWorkOfferService.Object);

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
        var handler = CreateHandler(queuedVoiceWorkOfferService.Object);

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
    public async Task HandleAsync_WhenPresenceChangesToAvailable_OffersQueuedVoiceWorkForAgent()
    {
        // Arrange
        var queuedVoiceWorkOfferService = new Mock<IQueuedVoiceWorkOfferService>();
        var handler = CreateHandler(queuedVoiceWorkOfferService.Object);
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.AgentPresenceChanged,
            AggregateId = "a1",
        };
        interactionEvent.SetData(new AgentPresenceChangedEventData
        {
            PreviousStatus = AgentPresenceStatus.Break,
            CurrentStatus = AgentPresenceStatus.Available,
        });

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        queuedVoiceWorkOfferService.Verify(service => service.OfferForAgentAsync("a1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenPresenceChangesToNotReady_DoesNotOfferQueuedVoiceWork()
    {
        // Arrange
        var queuedVoiceWorkOfferService = new Mock<IQueuedVoiceWorkOfferService>();
        var handler = CreateHandler(queuedVoiceWorkOfferService.Object);
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.AgentPresenceChanged,
            AggregateId = "a1",
        };
        interactionEvent.SetData(new AgentPresenceChangedEventData
        {
            PreviousStatus = AgentPresenceStatus.Available,
            CurrentStatus = AgentPresenceStatus.Break,
        });

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        queuedVoiceWorkOfferService.Verify(
        service => service.OfferForAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
        Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenTheVoiceFeatureIsNotEnabled_CompletesWithoutOfferingAnything()
    {
        // Arrange
        // A tenant without Voice resolves the do-nothing default rather than nothing at all, so the handler runs
        // to completion and simply has no work to offer.
        var handler = CreateHandler(new NoQueuedVoiceWorkOfferService());

        // Act
        await handler.HandleAsync(new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.AgentSignedIn,
            AggregateId = "a1",
        }, TestContext.Current.CancellationToken);

        // Assert
        // Reaching here is the assertion: nothing threw, and no offer could have been made.
    }

    private static OfferQueuedVoiceWorkOnAvailabilityHandler CreateHandler(
        IQueuedVoiceWorkOfferService queuedVoiceWorkOfferService = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton(queuedVoiceWorkOfferService ?? new NoQueuedVoiceWorkOfferService());

        services.AddTransient<QueuedVoiceWorkOfferScopeContext>();
        var serviceProvider = services.BuildServiceProvider();

        return new OfferQueuedVoiceWorkOnAvailabilityHandler(
            new TestContactCenterScopeExecutor(serviceProvider));
    }
}
