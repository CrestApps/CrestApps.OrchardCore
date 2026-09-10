using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using Moq;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterWorkflowEventHandlerTests
{
    [Fact]
    public void ReplaySafety_IsDeduplicatedByEventId()
    {
        // Arrange
        var handler = new ContactCenterWorkflowEventHandler(
            new Mock<IWorkflowManager>().Object,
            new Mock<IContactCenterEventDeduplicationService>().Object);

        // Act
        var replaySafety = handler.ReplaySafety;

        // Assert
        Assert.Equal(ContactCenterHandlerReplaySafety.DeduplicatedByEventId, replaySafety);
    }

    [Fact]
    public async Task HandleAsync_WhenEventIsNew_TriggersTheWorkflowEvent()
    {
        // Arrange
        var workflowManager = new Mock<IWorkflowManager>();
        var deduplication = new Mock<IContactCenterEventDeduplicationService>();
        deduplication
            .Setup(service => service.TryBeginAsync("ContactCenter/WorkflowBridge/v1", "event-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new ContactCenterWorkflowEventHandler(workflowManager.Object, deduplication.Object);
        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-1",
            EventType = "OfferAccepted",
            InteractionId = "interaction-1",
        };

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        workflowManager.Verify(
            manager => manager.TriggerEventAsync(
                nameof(ContactCenterEvent),
                It.IsAny<IDictionary<string, object>>(),
                "interaction-1",
                It.IsAny<bool>(),
                It.IsAny<bool>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenEventIsReplayed_DoesNotStartADuplicateWorkflow()
    {
        // Arrange
        var workflowManager = new Mock<IWorkflowManager>();
        var deduplication = new Mock<IContactCenterEventDeduplicationService>();
        deduplication
            .SetupSequence(service => service.TryBeginAsync("ContactCenter/WorkflowBridge/v1", "event-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        var handler = new ContactCenterWorkflowEventHandler(workflowManager.Object, deduplication.Object);
        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-1",
            EventType = "OfferAccepted",
            InteractionId = "interaction-1",
        };

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        workflowManager.Verify(
            manager => manager.TriggerEventAsync(
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()),
            Times.Once);
    }
}
