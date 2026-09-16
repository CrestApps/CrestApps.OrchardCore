using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class RecordingMediaDeletionHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenRecordingErasedWithReference_DeletesMediaAndPublishesConfirmation()
    {
        // Arrange
        var mediaStore = new Mock<IRecordingMediaStore>();
        mediaStore
            .Setup(m => m.DeleteAsync("storage/int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var publisher = new Mock<IContactCenterEventPublisher>();
        InteractionEvent published = null;
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(mediaStore.Object, publisher.Object);
        var erasedEvent = CreateErasedEvent("storage/int1");

        // Act
        await handler.HandleAsync(erasedEvent, TestContext.Current.CancellationToken);

        // Assert
        mediaStore.Verify(m => m.DeleteAsync("storage/int1", It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(published);
        Assert.Equal(ContactCenterConstants.Events.RecordingMediaDeleted, published.EventType);

        var data = published.GetData<RecordingMediaDeletedEventData>();

        Assert.Equal("dpo-1", data.ActorId);
        Assert.Equal("storage/int1", data.RecordingReference);
    }

    [Fact]
    public async Task HandleAsync_WhenDeleteCannotBeConfirmed_ThrowsForOutboxRetry()
    {
        // Arrange
        var mediaStore = new Mock<IRecordingMediaStore>();
        mediaStore
            .Setup(m => m.DeleteAsync("storage/int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var publisher = new Mock<IContactCenterEventPublisher>();

        var handler = CreateHandler(mediaStore.Object, publisher.Object);
        var erasedEvent = CreateErasedEvent("storage/int1");

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(erasedEvent, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("could not be confirmed", exception.Message, StringComparison.Ordinal);
        mediaStore.Verify(m => m.DeleteAsync("storage/int1", It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenEventIsNotRecordingErased_DoesNothing()
    {
        // Arrange
        var mediaStore = new Mock<IRecordingMediaStore>();
        var publisher = new Mock<IContactCenterEventPublisher>();

        var handler = CreateHandler(mediaStore.Object, publisher.Object);
        var otherEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.RecordingAccessed,
            InteractionId = "int1",
        };

        // Act
        await handler.HandleAsync(otherEvent, TestContext.Current.CancellationToken);

        // Assert
        mediaStore.Verify(m => m.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenRecordingReferenceMissing_DoesNothing()
    {
        // Arrange
        var mediaStore = new Mock<IRecordingMediaStore>();
        var publisher = new Mock<IContactCenterEventPublisher>();

        var handler = CreateHandler(mediaStore.Object, publisher.Object);
        var erasedEvent = CreateErasedEvent(recordingReference: null);

        // Act
        await handler.HandleAsync(erasedEvent, TestContext.Current.CancellationToken);

        // Assert
        mediaStore.Verify(m => m.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static RecordingMediaDeletionHandler CreateHandler(
        IRecordingMediaStore mediaStore,
        IContactCenterEventPublisher publisher)
    {
        var services = new ServiceCollection();
        services.AddSingleton(publisher);

        return new RecordingMediaDeletionHandler(
            mediaStore,
            services.BuildServiceProvider(),
            NullLogger<RecordingMediaDeletionHandler>.Instance);
    }

    private static InteractionEvent CreateErasedEvent(string recordingReference)
    {
        var erasedEvent = new InteractionEvent
        {
            ItemId = "event-erased-1",
            EventType = ContactCenterConstants.Events.RecordingErased,
            InteractionId = "int1",
            AggregateType = nameof(Interaction),
            AggregateId = "int1",
            ActorId = "dpo-1",
        };

        erasedEvent.SetData(new RecordingErasedEventData
        {
            ActorId = "dpo-1",
            Reason = "gdpr-subject-request",
            RecordingReference = recordingReference,
        });

        return erasedEvent;
    }
}
