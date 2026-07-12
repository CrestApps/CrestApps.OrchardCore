using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterSoftPhoneEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_CallConnected_CreatesTelephonyInteraction_AndPushesConnectedState()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "Asterisk",
            ProviderInteractionId = "call-1",
            CustomerAddress = "+15550001000",
            QueueId = "queue-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Outbound,
            Status = InteractionStatus.Connected,
            CreatedUtc = new DateTime(2026, 7, 10, 13, 0, 0, DateTimeKind.Utc),
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 5, DateTimeKind.Utc),
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "Asterisk",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Outbound,
            State = ContactCenterCallState.Connected,
            FromAddress = "+15550002000",
            ToAddress = "+15550001000",
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 5, DateTimeKind.Utc),
            AnsweredUtc = new DateTime(2026, 7, 10, 13, 0, 9, DateTimeKind.Utc),
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager.Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(manager => manager.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "agent-1",
                UserId = "user-1",
                UserName = "agent.one",
            });

        var store = new Mock<ITelephonyInteractionStore>();
        store.Setup(value => value.FindByCallIdAsync("user-1", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelephonyInteraction)null);

        TelephonyInteraction createdInteraction = null;
        store.Setup(value => value.CreateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()))
            .Callback<TelephonyInteraction, CancellationToken>((telephonyInteraction, _) => createdInteraction = telephonyInteraction)
            .Returns(Task.CompletedTask);

        var client = new Mock<ITelephonyClient>();
        var clients = new Mock<IHubClients<ITelephonyClient>>();
        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        clients.Setup(value => value.User("user-1")).Returns(client.Object);
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        var handler = new ContactCenterSoftPhoneEventHandler(
            interactionManager.Object,
            callSessionManager.Object,
            agentManager.Object,
            store.Object,
            hubContext.Object);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.CallConnected,
            InteractionId = "interaction-1",
        };

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(createdInteraction);
        Assert.Equal("interaction-1", createdInteraction.InteractionId);
        Assert.Equal(CallOutcome.InProgress, createdInteraction.Outcome);
        Assert.Null(createdInteraction.EndedUtc);

        client.Verify(
            value => value.CallStateChanged(It.Is<TelephonyCall>(call =>
                call.CallId == "call-1" &&
                call.State == CallState.Connected &&
                call.Direction == CallDirection.Outbound)),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CallEnded_UpdatesTelephonyInteraction_AndPushesDisconnectedState()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "Asterisk",
            ProviderInteractionId = "call-1",
            CustomerAddress = "+15550001000",
            QueueId = "queue-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Outbound,
            Status = InteractionStatus.Ended,
            CreatedUtc = new DateTime(2026, 7, 10, 13, 0, 0, DateTimeKind.Utc),
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 5, DateTimeKind.Utc),
            EndedUtc = new DateTime(2026, 7, 10, 13, 1, 0, DateTimeKind.Utc),
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "Asterisk",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Outbound,
            State = ContactCenterCallState.Ended,
            FromAddress = "+15550002000",
            ToAddress = "+15550001000",
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 5, DateTimeKind.Utc),
            EndedUtc = new DateTime(2026, 7, 10, 13, 1, 0, DateTimeKind.Utc),
        };
        var existing = new TelephonyInteraction
        {
            InteractionId = "interaction-1",
            CallId = "call-1",
            UserId = "user-1",
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 5, DateTimeKind.Utc),
            Outcome = CallOutcome.InProgress,
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager.Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(manager => manager.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "agent-1",
                UserId = "user-1",
                UserName = "agent.one",
            });

        var store = new Mock<ITelephonyInteractionStore>();
        store.Setup(value => value.FindByCallIdAsync("user-1", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var client = new Mock<ITelephonyClient>();
        var clients = new Mock<IHubClients<ITelephonyClient>>();
        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        clients.Setup(value => value.User("user-1")).Returns(client.Object);
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        var handler = new ContactCenterSoftPhoneEventHandler(
            interactionManager.Object,
            callSessionManager.Object,
            agentManager.Object,
            store.Object,
            hubContext.Object);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.CallEnded,
            InteractionId = "interaction-1",
        };

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CallOutcome.Completed, existing.Outcome);
        Assert.Equal(new DateTime(2026, 7, 10, 13, 1, 0, DateTimeKind.Utc), existing.EndedUtc);
        Assert.Equal(55, existing.DurationSeconds);

        store.Verify(value => value.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(
            value => value.CallStateChanged(It.Is<TelephonyCall>(call =>
                call.CallId == "call-1" &&
                call.State == CallState.Disconnected)),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CallSessionUpdated_PushesMutedHoldState()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "Asterisk",
            ProviderInteractionId = "call-1",
            CustomerAddress = "+15550001000",
            QueueId = "queue-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Outbound,
            Status = InteractionStatus.Held,
            CreatedUtc = new DateTime(2026, 7, 10, 13, 0, 0, DateTimeKind.Utc),
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 5, DateTimeKind.Utc),
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "Asterisk",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Outbound,
            State = ContactCenterCallState.OnHold,
            IsOnHold = true,
            IsMuted = true,
            FromAddress = "+15550002000",
            ToAddress = "+15550001000",
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 5, DateTimeKind.Utc),
            AnsweredUtc = new DateTime(2026, 7, 10, 13, 0, 9, DateTimeKind.Utc),
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager.Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(manager => manager.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "agent-1",
                UserId = "user-1",
                UserName = "agent.one",
            });

        var store = new Mock<ITelephonyInteractionStore>();
        store.Setup(value => value.FindByCallIdAsync("user-1", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelephonyInteraction)null);

        var client = new Mock<ITelephonyClient>();
        var clients = new Mock<IHubClients<ITelephonyClient>>();
        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        clients.Setup(value => value.User("user-1")).Returns(client.Object);
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        var handler = new ContactCenterSoftPhoneEventHandler(
            interactionManager.Object,
            callSessionManager.Object,
            agentManager.Object,
            store.Object,
            hubContext.Object);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.CallSessionUpdated,
            InteractionId = "interaction-1",
        };

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        client.Verify(
            value => value.CallStateChanged(It.Is<TelephonyCall>(call =>
                call.CallId == "call-1" &&
                call.State == CallState.OnHold &&
                call.IsOnHold &&
                call.IsMuted)),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CallSessionUpdated_WhenInboundOfferIsRinging_PushesRingingState()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "Asterisk",
            ProviderInteractionId = "call-1",
            CustomerAddress = "+15550001000",
            QueueId = "queue-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Inbound,
            Status = InteractionStatus.Ringing,
            CreatedUtc = new DateTime(2026, 7, 10, 13, 0, 0, DateTimeKind.Utc),
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "Asterisk",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Inbound,
            State = ContactCenterCallState.Connected,
            FromAddress = "+15550001000",
            ToAddress = "+15550002000",
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 0, DateTimeKind.Utc),
            AnsweredUtc = new DateTime(2026, 7, 10, 13, 0, 1, DateTimeKind.Utc),
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager.Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(manager => manager.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "agent-1",
                UserId = "user-1",
                UserName = "agent.one",
            });

        var store = new Mock<ITelephonyInteractionStore>();
        store.Setup(value => value.FindByCallIdAsync("user-1", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelephonyInteraction)null);

        var client = new Mock<ITelephonyClient>();
        var clients = new Mock<IHubClients<ITelephonyClient>>();
        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        clients.Setup(value => value.User("user-1")).Returns(client.Object);
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        var handler = new ContactCenterSoftPhoneEventHandler(
            interactionManager.Object,
            callSessionManager.Object,
            agentManager.Object,
            store.Object,
            hubContext.Object);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.CallSessionUpdated,
            InteractionId = "interaction-1",
        };

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        client.Verify(
            value => value.CallStateChanged(It.Is<TelephonyCall>(call =>
                call.CallId == "call-1" &&
                call.State == CallState.Ringing &&
                call.Direction == CallDirection.Inbound)),
            Times.Once);
    }
}
