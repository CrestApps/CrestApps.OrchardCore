using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Bug: the agent workspace and the docked agent bar never showed an outbound dialer call while it rang and talked.
/// The dial is accepted before it is placed, so the refresh the acceptance caused found nothing live, and nothing told
/// the agent's screens about the call again until wrap-up. The call's own events are now pushed to the agent as
/// InteractionChanged. Confirmed live; these pin who is told and when.
/// </summary>
public sealed class ContactCenterInteractionChangedPushTests
{
    private static readonly DateTime _now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(ContactCenterConstants.Events.DialStarted, InteractionStatus.Ringing)]
    [InlineData(ContactCenterConstants.Events.CallConnected, InteractionStatus.Connected)]
    [InlineData(ContactCenterConstants.Events.CallHeld, InteractionStatus.Held)]
    [InlineData(ContactCenterConstants.Events.CallResumed, InteractionStatus.Connected)]
    [InlineData(ContactCenterConstants.Events.CallEnded, InteractionStatus.Ended)]
    public async Task HandleAsync_ACallEventOnTheAgentsInteraction_PushesInteractionChangedToTheAgentsUser(string eventType, InteractionStatus status)
    {
        // Arrange
        var harness = new HandlerHarness(new Interaction
        {
            ItemId = "interaction-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Outbound,
        }.RestorePersistedStatus(status));

        // Act
        await harness.Handler.HandleAsync(new InteractionEvent
        {
            EventType = eventType,
            AggregateId = "interaction-1",
            InteractionId = "interaction-1",
            OccurredUtc = _now,
        }, TestContext.Current.CancellationToken);

        // Assert
        harness.Notifier.Verify(
            notifier => notifier.NotifyInteractionChangedAsync(
                It.Is<AgentInteractionNotification>(notification =>
                    notification.UserId == "user-1" &&
                    notification.AgentId == "agent-1" &&
                    notification.InteractionId == "interaction-1" &&
                    notification.EventType == eventType &&
                    notification.Status == status.ToString() &&
                    notification.Direction == nameof(InteractionDirection.Outbound) &&
                    notification.ServerTimeUtc == _now),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Call events are recorded against the interaction; an event that carries its id only as the aggregate must still
    // reach the agent.
    [Fact]
    public async Task HandleAsync_AnEventWithoutAnInteractionId_FindsTheInteractionByItsAggregateId()
    {
        // Arrange
        var harness = new HandlerHarness(new Interaction
        {
            ItemId = "interaction-1",
            AgentId = "agent-1",
        }.RestorePersistedStatus(InteractionStatus.Connected));

        // Act
        await harness.Handler.HandleAsync(new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.CallConnected,
            AggregateId = "interaction-1",
            OccurredUtc = _now,
        }, TestContext.Current.CancellationToken);

        // Assert
        harness.Notifier.Verify(
            notifier => notifier.NotifyInteractionChangedAsync(
                It.Is<AgentInteractionNotification>(notification => notification.InteractionId == "interaction-1" && notification.UserId == "user-1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // A call still in the phone menu or waiting in a queue has no agent; its screens are told through the offer.
    [Fact]
    public async Task HandleAsync_AnInteractionWithNoAgent_PushesNothing()
    {
        // Arrange
        var harness = new HandlerHarness(new Interaction { ItemId = "interaction-1", AgentId = null });

        // Act
        await harness.Handler.HandleAsync(CallConnected(), TestContext.Current.CancellationToken);

        // Assert
        harness.Notifier.Verify(
            notifier => notifier.NotifyInteractionChangedAsync(It.IsAny<AgentInteractionNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AnInteractionThatDoesNotExist_PushesNothing()
    {
        // Arrange
        var harness = new HandlerHarness(interaction: null);

        // Act
        await harness.Handler.HandleAsync(CallConnected(), TestContext.Current.CancellationToken);

        // Assert
        harness.Notifier.Verify(
            notifier => notifier.NotifyInteractionChangedAsync(It.IsAny<AgentInteractionNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_AnAgentWithNoUser_PushesNothing(bool agentExists)
    {
        // Arrange
        var harness = new HandlerHarness(
            new Interaction { ItemId = "interaction-1", AgentId = "agent-1" },
            agent: agentExists ? new AgentProfile { ItemId = "agent-1", UserId = null } : null);

        // Act
        await harness.Handler.HandleAsync(CallConnected(), TestContext.Current.CancellationToken);

        // Assert
        harness.Notifier.Verify(
            notifier => notifier.NotifyInteractionChangedAsync(It.IsAny<AgentInteractionNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // The push is for the agent's own screens; supervisors follow agents through presence and their own board.
    [Fact]
    public async Task NotifyInteractionChangedAsync_SendsOnlyToTheAgentsUserGroup()
    {
        // Arrange
        var userClient = new Mock<IContactCenterHubClient>();
        var otherClient = new Mock<IContactCenterHubClient>();
        var clients = new Mock<IHubClients<IContactCenterHubClient>>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(otherClient.Object);
        clients.Setup(c => c.Group(TenantSignalRGroupName.ForUser("TenantA", "user-1"))).Returns(userClient.Object);
        clients.SetupGet(c => c.All).Returns(otherClient.Object);

        var hubContext = new Mock<IHubContext<ContactCenterHub, IContactCenterHubClient>>();
        hubContext.SetupGet(c => c.Clients).Returns(clients.Object);

        var notifier = new ContactCenterRealTimeNotifier(
            hubContext.Object,
            new Mock<IAgentSessionManager>().Object,
            new ShellSettings { Name = "TenantA" });

        var notification = new AgentInteractionNotification
        {
            InteractionId = "interaction-1",
            UserId = "user-1",
            AgentId = "agent-1",
            EventType = ContactCenterConstants.Events.CallConnected,
        };

        // Act
        await notifier.NotifyInteractionChangedAsync(notification, TestContext.Current.CancellationToken);

        // Assert
        userClient.Verify(client => client.InteractionChanged(notification), Times.Once);
        otherClient.Verify(client => client.InteractionChanged(It.IsAny<AgentInteractionNotification>()), Times.Never);
        clients.Verify(c => c.Group(It.IsAny<string>()), Times.Once);
        clients.Verify(c => c.Group(TenantSignalRGroupName.ForGroup("TenantA", ContactCenterHub.SupervisorsGroup)), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task NotifyInteractionChangedAsync_WithoutAUser_SendsNothing(string userId)
    {
        // Arrange
        var clients = new Mock<IHubClients<IContactCenterHubClient>>();
        var hubContext = new Mock<IHubContext<ContactCenterHub, IContactCenterHubClient>>();
        hubContext.SetupGet(c => c.Clients).Returns(clients.Object);

        var notifier = new ContactCenterRealTimeNotifier(
            hubContext.Object,
            new Mock<IAgentSessionManager>().Object,
            new ShellSettings { Name = "TenantA" });

        // Act
        await notifier.NotifyInteractionChangedAsync(
            new AgentInteractionNotification { InteractionId = "interaction-1", UserId = userId },
            TestContext.Current.CancellationToken);

        // Assert
        clients.Verify(c => c.Group(It.IsAny<string>()), Times.Never);
    }

    private static InteractionEvent CallConnected()
        => new()
        {
            EventType = ContactCenterConstants.Events.CallConnected,
            AggregateId = "interaction-1",
            InteractionId = "interaction-1",
            OccurredUtc = _now,
        };

    private sealed class HandlerHarness
    {
        public HandlerHarness(Interaction interaction)
            : this(interaction, new AgentProfile { ItemId = "agent-1", UserId = "user-1" })
        {
        }

        public HandlerHarness(Interaction interaction, AgentProfile agent)
        {
            var interactionManager = new Mock<IInteractionManager>();
            interactionManager
                .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(interaction);

            var agentManager = new Mock<IAgentProfileManager>();
            agentManager
                .Setup(manager => manager.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(agent);

            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(_now);

            var provider = new ServiceCollection()
                .AddSingleton(agentManager.Object)
                .AddSingleton(new Mock<IActivityReservationManager>().Object)
                .AddSingleton(new Mock<IQueueItemStore>().Object)
                .AddSingleton(new Mock<IOmnichannelActivityManager>().Object)
                .AddSingleton(interactionManager.Object)
                .AddSingleton(new Mock<UserManager<IUser>>(new Mock<IUserStore<IUser>>().Object, null, null, null, null, null, null, null, null).Object)
                .AddSingleton(new Mock<IDisplayNameProvider>().Object)
                .AddTransient<ContactCenterRealTimeEventScopeContext>()
                .BuildServiceProvider();

            Handler = new ContactCenterRealTimeEventHandler(
                Notifier.Object,
                new TestContactCenterScopeExecutor(provider),
                clock.Object,
                NullLogger<ContactCenterRealTimeEventHandler>.Instance);
        }

        public Mock<IContactCenterRealTimeNotifier> Notifier { get; } = new();

        public ContactCenterRealTimeEventHandler Handler { get; }
    }
}
