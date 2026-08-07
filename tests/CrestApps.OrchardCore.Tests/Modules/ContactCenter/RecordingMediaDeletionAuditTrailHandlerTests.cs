using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using Moq;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class RecordingMediaDeletionAuditTrailHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenDeletionIsConfirmed_RecordsAuditTrailReceipt()
    {
        // Arrange
        var auditTrailManager = new Mock<IAuditTrailManager>();
        AuditTrailContext<RecordingMediaDeletedEventData> recorded = null;
        auditTrailManager
            .Setup(manager => manager.RecordEventAsync(It.IsAny<AuditTrailContext<RecordingMediaDeletedEventData>>()))
            .Callback<AuditTrailContext<RecordingMediaDeletedEventData>>(context => recorded = context)
            .Returns(Task.CompletedTask);
        var deduplicationService = new Mock<IContactCenterEventDeduplicationService>();
        deduplicationService
            .Setup(service => service.TryBeginAsync(
                "ContactCenter/RecordingMediaDeletionAuditTrail/v1",
                "event-deleted-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new RecordingMediaDeletionAuditTrailHandler(
            auditTrailManager.Object,
            deduplicationService.Object);
        var interactionEvent = CreateEvent();

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(recorded);
        Assert.Equal("ContactCenter", recorded.Category);
        Assert.Equal("RecordingMediaDeleted", recorded.Name);
        Assert.Equal("interaction-1", recorded.CorrelationId);
        Assert.Equal("dpo-1", recorded.UserId);
        Assert.Equal("storage/interaction-1", recorded.AuditTrailEventItem.RecordingReference);
        Assert.Equal("gdpr-subject-request", recorded.AuditTrailEventItem.Reason);
    }

    [Fact]
    public async Task HandleAsync_WhenEventWasAlreadyAudited_DoesNotRecordDuplicate()
    {
        // Arrange
        var auditTrailManager = new Mock<IAuditTrailManager>();
        var deduplicationService = new Mock<IContactCenterEventDeduplicationService>();
        deduplicationService
            .Setup(service => service.TryBeginAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = new RecordingMediaDeletionAuditTrailHandler(
            auditTrailManager.Object,
            deduplicationService.Object);

        // Act
        await handler.HandleAsync(CreateEvent(), TestContext.Current.CancellationToken);

        // Assert
        auditTrailManager.Verify(
            manager => manager.RecordEventAsync(It.IsAny<AuditTrailContext<RecordingMediaDeletedEventData>>()),
            Times.Never);
    }

    private static InteractionEvent CreateEvent()
    {
        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-deleted-1",
            EventType = ContactCenterConstants.Events.RecordingMediaDeleted,
            InteractionId = "interaction-1",
            ActorId = "dpo-1",
        };

        interactionEvent.SetData(new RecordingMediaDeletedEventData
        {
            ActorId = "dpo-1",
            Reason = "gdpr-subject-request",
            RecordingReference = "storage/interaction-1",
        });

        return interactionEvent;
    }
}
