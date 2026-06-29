using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class DefaultContactCenterEventPublisherTests
{
    private static readonly DateTime _now = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task PublishAsync_PersistsTheEvent_AndDispatchesToHandlers()
    {
        // Arrange
        var store = CreateStore();
        var handler = new Mock<IContactCenterEventHandler>();
        handler
            .Setup(h => h.HandleAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var publisher = CreatePublisher(store.Object, [handler.Object]);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionCreated,
            InteractionId = "interaction-1",
        };

        // Act
        await publisher.PublishAsync(interactionEvent, CancellationToken.None);

        // Assert
        store.Verify(s => s.CreateAsync(interactionEvent, It.IsAny<CancellationToken>()), Times.Once);
        handler.Verify(h => h.HandleAsync(interactionEvent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WhenOccurredUtcIsDefault_StampsFromClock()
    {
        // Arrange
        var store = CreateStore();
        var publisher = CreatePublisher(store.Object, []);
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionCreated,
        };

        // Act
        await publisher.PublishAsync(interactionEvent, CancellationToken.None);

        // Assert
        Assert.Equal(_now, interactionEvent.OccurredUtc);
    }

    [Fact]
    public async Task PublishAsync_WhenActorIdIsEmpty_SetsTheSystemActor()
    {
        // Arrange
        var store = CreateStore();
        var publisher = CreatePublisher(store.Object, []);
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionCreated,
        };

        // Act
        await publisher.PublishAsync(interactionEvent, CancellationToken.None);

        // Assert
        Assert.Equal(ContactCenterConstants.SystemActor, interactionEvent.ActorId);
    }

    [Fact]
    public async Task PublishAsync_WhenIdempotencyKeyAlreadyExists_SkipsPersistAndDispatch()
    {
        // Arrange
        var store = CreateStore();
        store
            .Setup(s => s.ExistsByIdempotencyKeyAsync("dup-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new Mock<IContactCenterEventHandler>();
        var publisher = CreatePublisher(store.Object, [handler.Object]);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionCreated,
            IdempotencyKey = "dup-key",
        };

        // Act
        await publisher.PublishAsync(interactionEvent, CancellationToken.None);

        // Assert
        store.Verify(s => s.CreateAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        handler.Verify(h => h.HandleAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_WhenAHandlerThrows_DoesNotThrow_AndStillRunsOtherHandlers()
    {
        // Arrange
        var store = CreateStore();

        var faultyHandler = new Mock<IContactCenterEventHandler>();
        faultyHandler
            .Setup(h => h.HandleAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var healthyHandler = new Mock<IContactCenterEventHandler>();
        healthyHandler
            .Setup(h => h.HandleAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var publisher = CreatePublisher(store.Object, [faultyHandler.Object, healthyHandler.Object]);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionCreated,
        };

        // Act
        await publisher.PublishAsync(interactionEvent, CancellationToken.None);

        // Assert
        store.Verify(s => s.CreateAsync(interactionEvent, It.IsAny<CancellationToken>()), Times.Once);
        healthyHandler.Verify(h => h.HandleAsync(interactionEvent, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IInteractionEventStore> CreateStore()
    {
        var store = new Mock<IInteractionEventStore>();
        store
            .Setup(s => s.CreateAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        store
            .Setup(s => s.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        return store;
    }

    private static DefaultContactCenterEventPublisher CreatePublisher(
        IInteractionEventStore store,
        IEnumerable<IContactCenterEventHandler> handlers)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new DefaultContactCenterEventPublisher(
            store,
            handlers,
            clock.Object,
            NullLogger<DefaultContactCenterEventPublisher>.Instance);
    }
}
