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
    public async Task PublishAsync_PersistsTheEvent_AndDispatchesThroughTheOutbox()
    {
        // Arrange
        var store = CreateStore();
        var outbox = new Mock<IContactCenterOutbox>();
        var publisher = CreatePublisher(store.Object, outbox.Object);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionCreated,
            InteractionId = "interaction-1",
        };

        // Act
        await publisher.PublishAsync(interactionEvent, CancellationToken.None);

        // Assert
        store.Verify(s => s.CreateAsync(interactionEvent, It.IsAny<CancellationToken>()), Times.Once);
        outbox.Verify(o => o.DispatchAsync(interactionEvent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WhenOccurredUtcIsDefault_StampsFromClock()
    {
        // Arrange
        var store = CreateStore();
        var publisher = CreatePublisher(store.Object, new Mock<IContactCenterOutbox>().Object);
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
    public async Task PublishAsync_WhenItemIdIsEmpty_AssignsAnId()
    {
        // Arrange
        var store = CreateStore();
        var publisher = CreatePublisher(store.Object, new Mock<IContactCenterOutbox>().Object);
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionCreated,
        };

        // Act
        await publisher.PublishAsync(interactionEvent, CancellationToken.None);

        // Assert
        Assert.False(string.IsNullOrEmpty(interactionEvent.ItemId));
    }

    [Fact]
    public async Task PublishAsync_WhenActorIdIsEmpty_SetsTheSystemActor()
    {
        // Arrange
        var store = CreateStore();
        var publisher = CreatePublisher(store.Object, new Mock<IContactCenterOutbox>().Object);
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

        var outbox = new Mock<IContactCenterOutbox>();
        var publisher = CreatePublisher(store.Object, outbox.Object);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionCreated,
            IdempotencyKey = "dup-key",
        };

        // Act
        await publisher.PublishAsync(interactionEvent, CancellationToken.None);

        // Assert
        store.Verify(s => s.CreateAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        outbox.Verify(o => o.DispatchAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
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
        IContactCenterOutbox outbox)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new DefaultContactCenterEventPublisher(
            store,
            outbox,
            clock.Object,
            NullLogger<DefaultContactCenterEventPublisher>.Instance);
    }
}
