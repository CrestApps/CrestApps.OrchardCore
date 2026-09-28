using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
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

    [Fact]
    public async Task HandleAsync_APresenceChangeThePlatformMade_StillGivesTheWorkflowTheAgent()
    {
        // Arrange
        // A workflow reacting to an agent's presence used to read the agent from ActorId, because the agent was
        // put there whoever made the change. The platform now names itself there when it made the change, so the
        // agent the event is about is given to the workflow on its own.
        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-1",
            EventType = ContactCenterConstants.Events.AgentPresenceChanged,
            AggregateType = nameof(AgentProfile),
            AggregateId = "a1",
            ActorId = ContactCenterConstants.SystemActor,
            ActorType = ContactCenterActorType.System,
        };
        interactionEvent.SetData(new AgentPresenceChangedEventData { AgentId = "a1", UserId = "u1" });

        // Act
        var input = await TriggerAsync(interactionEvent);

        // Assert
        Assert.Equal("a1", input["AgentId"]);
        Assert.Equal("u1", input["AgentUserId"]);
        Assert.Equal(ContactCenterConstants.SystemActor, input["ActorId"]);
        Assert.Equal(nameof(ContactCenterActorType.System), input["ActorType"]);
    }

    [Fact]
    public async Task HandleAsync_AReservationRoutingMade_GivesTheWorkflowTheReservedAgent()
    {
        // Arrange
        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-1",
            EventType = ContactCenterConstants.Events.AgentReserved,
            AggregateType = nameof(ActivityReservation),
            AggregateId = "r1",
            ActorId = ContactCenterConstants.SystemActor,
            ActorType = ContactCenterActorType.System,
        };
        interactionEvent.SetData(new OfferLifecycleEventData { ReservationId = "r1", AgentId = "a1", UserId = "u1" });

        // Act
        var input = await TriggerAsync(interactionEvent);

        // Assert
        Assert.Equal("a1", input["AgentId"]);
        Assert.Equal("u1", input["AgentUserId"]);
    }

    private static async Task<IDictionary<string, object>> TriggerAsync(InteractionEvent interactionEvent)
    {
        IDictionary<string, object> input = null;
        var workflowManager = new Mock<IWorkflowManager>();
        workflowManager
            .Setup(manager => manager.TriggerEventAsync(
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Callback((string _, IDictionary<string, object> value, string _, bool _, bool _) => input = value);
        var deduplication = new Mock<IContactCenterEventDeduplicationService>();
        deduplication
            .Setup(service => service.TryBeginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new ContactCenterWorkflowEventHandler(workflowManager.Object, deduplication.Object);

        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        Assert.NotNull(input);

        return input;
    }
}
