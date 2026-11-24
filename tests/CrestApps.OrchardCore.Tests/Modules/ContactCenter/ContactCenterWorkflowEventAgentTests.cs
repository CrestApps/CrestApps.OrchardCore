using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.ContactCenter.Workflows.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The Contact Center Event names the agent the event is about by user name, including the agent the dialer placed a
/// call for when the event's own payload names nobody.
/// </summary>
public sealed class ContactCenterWorkflowEventAgentTests
{
    [Fact]
    public async Task HandleAsync_WhenADialerEventNamesNoAgent_PassesTheAgentTheCallWasPlacedFor()
    {
        // Arrange: a dialer attempt event carries no payload; the call records the agent the dialer reserved.
        var interactions = new Mock<IInteractionManager>();
        interactions
            .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction { ItemId = "interaction-1", AgentId = "agent-1" });

        var agents = new Mock<IAgentProfileManager>();
        agents
            .Setup(manager => manager.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });

        var (handler, input) = CreateHandler(interactions.Object, agents.Object, new User { UserId = "user-1", UserName = "jane" });

        // Act
        await handler.HandleAsync(
            new InteractionEvent
            {
                ItemId = "event-1",
                EventType = ContactCenterConstants.Events.DialerAttemptStarted,
                InteractionId = "interaction-1",
                ActorType = ContactCenterActorType.System,
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("agent-1", input.Value["AgentId"]);
        Assert.Equal("user-1", input.Value["AgentUserId"]);
        Assert.Equal("jane", input.Value["AgentUserName"]);
    }

    [Fact]
    public async Task HandleAsync_WhenTheDispositionPayloadNamesTheAgentUser_PassesThatUserOverTheActor()
    {
        // Arrange: the system applied the disposition of an automated dial; the payload names the agent held for it.
        var interactions = new Mock<IInteractionManager>();
        var (handler, input) = CreateHandler(interactions.Object, Mock.Of<IAgentProfileManager>(), new User { UserId = "user-2", UserName = "omar" });
        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-2",
            EventType = ContactCenterConstants.Events.ActivityDispositionApplied,
            InteractionId = "interaction-2",
            ActorType = ContactCenterActorType.System,
        };
        interactionEvent.SetData(new ActivityDispositionEventData { ActivityItemId = "activity-1", AgentUserId = "user-2" });

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert: the payload already named the user, so the call was not read for it.
        Assert.Equal("user-2", input.Value["AgentUserId"]);
        Assert.Equal("omar", input.Value["AgentUserName"]);
        interactions.Verify(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenNoAgentIsKnown_PassesNoUserName()
    {
        // Arrange: a predictive call nobody answered never had an agent.
        var interactions = new Mock<IInteractionManager>();
        interactions
            .Setup(manager => manager.FindByIdAsync("interaction-3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction { ItemId = "interaction-3" });

        var (handler, input) = CreateHandler(interactions.Object, Mock.Of<IAgentProfileManager>(), user: null);

        // Act
        await handler.HandleAsync(
            new InteractionEvent
            {
                ItemId = "event-3",
                EventType = ContactCenterConstants.Events.DialerAttemptCompleted,
                InteractionId = "interaction-3",
                ActorType = ContactCenterActorType.System,
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(input.Value["AgentId"]);
        Assert.Null(input.Value["AgentUserId"]);
        Assert.Null(input.Value["AgentUserName"]);
    }

    private static (ContactCenterWorkflowEventHandler Handler, StrongBox<IDictionary<string, object>> Input) CreateHandler(
        IInteractionManager interactionManager,
        IAgentProfileManager agentManager,
        User user)
    {
        var input = new StrongBox<IDictionary<string, object>>();
        var workflowManager = new Mock<IWorkflowManager>();
        workflowManager
            .Setup(manager => manager.TriggerEventAsync(nameof(ContactCenterEvent), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Callback((string _, IDictionary<string, object> value, string _, bool _, bool _) => input.Value = value)
            .ReturnsAsync([]);

        var deduplication = new Mock<IContactCenterEventDeduplicationService>();
        deduplication
            .Setup(service => service.TryBeginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var subscriptions = new Mock<IContactCenterWorkflowSubscriptions>();
        subscriptions.Setup(s => s.IsSubscribedAsync(It.IsAny<string>())).ReturnsAsync(true);

        var users = new Mock<IUserStore<IUser>>();
        users
            .Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => user is not null && user.UserId == id ? user : null);
        users
            .Setup(store => store.GetUserNameAsync(It.IsAny<IUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IUser value, CancellationToken _) => ((User)value).UserName);

        var handler = new ContactCenterWorkflowEventHandler(
            workflowManager.Object,
            deduplication.Object,
            subscriptions.Object,
            interactionManager,
            agentManager,
            new UserManager<IUser>(users.Object, null, null, null, null, null, null, null, NullLogger<UserManager<IUser>>.Instance));

        return (handler, input);
    }

    private sealed class StrongBox<T>
    {
        public T Value { get; set; }
    }
}
