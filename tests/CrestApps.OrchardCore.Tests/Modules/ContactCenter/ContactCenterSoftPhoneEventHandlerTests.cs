using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterSoftPhoneEventHandlerTests
{
    private static readonly ShellSettings _shellSettings = new()
    {
        Name = "TenantA",
    };

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
            CreatedUtc = new DateTime(2026, 7, 10, 13, 0, 0, DateTimeKind.Utc),
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 5, DateTimeKind.Utc),
        }.RestorePersistedStatus(InteractionStatus.Connected);
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "Asterisk",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Outbound,
            FromAddress = "+15550002000",
            ToAddress = "+15550001000",
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 5, DateTimeKind.Utc),
            AnsweredUtc = new DateTime(2026, 7, 10, 13, 0, 9, DateTimeKind.Utc),
        }.RestorePersistedState(VoiceCallState.Connected);

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
        clients.Setup(value => value.Group(TenantSignalRGroupName.ForUser(_shellSettings.Name, "user-1"))).Returns(client.Object);
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        var handler = new ContactCenterSoftPhoneEventHandler(
            interactionManager.Object,
            callSessionManager.Object,
            agentManager.Object,
            store.Object,
            hubContext.Object,
            _shellSettings);

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
            CreatedUtc = new DateTime(2026, 7, 10, 13, 0, 0, DateTimeKind.Utc),
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 5, DateTimeKind.Utc),
            EndedUtc = new DateTime(2026, 7, 10, 13, 1, 0, DateTimeKind.Utc),
        }.RestorePersistedStatus(InteractionStatus.Ended);
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "Asterisk",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Outbound,
            FromAddress = "+15550002000",
            ToAddress = "+15550001000",
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 5, DateTimeKind.Utc),
            EndedUtc = new DateTime(2026, 7, 10, 13, 1, 0, DateTimeKind.Utc),
        }.RestorePersistedState(VoiceCallState.Ended);
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
        clients.Setup(value => value.Group(TenantSignalRGroupName.ForUser(_shellSettings.Name, "user-1"))).Returns(client.Object);
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        var handler = new ContactCenterSoftPhoneEventHandler(
            interactionManager.Object,
            callSessionManager.Object,
            agentManager.Object,
            store.Object,
            hubContext.Object,
            _shellSettings);

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
    public async Task HandleAsync_CallSentToVoicemail_ProjectsMissedCall_AndResolvesRecipientFromMetadata()
    {
        // Arrange: the offer has released its agent association, so the recipient is resolved from voicemail
        // metadata rather than the session or interaction agent.
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "Telnyx",
            ProviderInteractionId = "call-1",
            CustomerAddress = "+15550001000",
            QueueId = ContactCenterConstants.DirectRouting.QueueId,
            AgentId = null,
            Direction = InteractionDirection.Inbound,
            CreatedUtc = new DateTime(2026, 7, 10, 13, 0, 0, DateTimeKind.Utc),
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 5, DateTimeKind.Utc),
            TechnicalMetadata = new Dictionary<string, object>
            {
                [ContactCenterConstants.Voicemail.ProjectionMetadataKey] = true,
                [ContactCenterConstants.Voicemail.RecipientAgentMetadataKey] = "agent-1",
            },
        }.RestorePersistedStatus(InteractionStatus.Ringing);

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager.Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CallSession)null);

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
        clients.Setup(value => value.Group(TenantSignalRGroupName.ForUser(_shellSettings.Name, "user-1"))).Returns(client.Object);
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        var handler = new ContactCenterSoftPhoneEventHandler(
            interactionManager.Object,
            callSessionManager.Object,
            agentManager.Object,
            store.Object,
            hubContext.Object,
            _shellSettings);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.CallSentToVoicemail,
            InteractionId = "interaction-1",
        };

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(createdInteraction);
        Assert.Equal(CallOutcome.Missed, createdInteraction.Outcome);
        Assert.True(createdInteraction.IsVoicemail);
        Assert.NotNull(createdInteraction.EndedUtc);

        client.Verify(
            value => value.CallStateChanged(It.Is<TelephonyCall>(call =>
                call.CallId == "call-1" &&
                call.State == CallState.Disconnected &&
                call.Direction == CallDirection.Inbound)),
            Times.Once);
    }

    [Theory]
    [InlineData(ContactCenterConstants.Events.CallSentToVoicemail)]
    [InlineData(ContactCenterConstants.Events.CallEnded)]
    public async Task HandleAsync_AMessageInAQueuesSharedBox_IsKeptOutOfEveryAgentsVoicemailTab(string eventType)
    {
        // Arrange
        // The caller was offered to an agent earlier, who let it ring out, and later reached voicemail from the queue.
        // The message belongs to the queue's team: it must not also land in that agent's personal Voicemail tab, which
        // is where the interaction's last agent would otherwise take it.
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "Telnyx",
            ProviderInteractionId = "call-1",
            CustomerAddress = "+15550001000",
            QueueId = "queue-main",
            AgentId = "agent-1",
            Direction = InteractionDirection.Inbound,
            CreatedUtc = new DateTime(2026, 7, 10, 13, 0, 0, DateTimeKind.Utc),
            TechnicalMetadata = new Dictionary<string, object>
            {
                [ContactCenterConstants.Voicemail.ProjectionMetadataKey] = true,
                [ContactCenterConstants.Voicemail.SharedQueueMetadataKey] = "queue-main",
            },
        }.RestorePersistedStatus(InteractionStatus.Ended);

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(manager => manager.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });

        var store = new Mock<ITelephonyInteractionStore>(MockBehavior.Strict);
        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>(MockBehavior.Strict);

        var handler = new ContactCenterSoftPhoneEventHandler(
            interactionManager.Object,
            new Mock<ICallSessionManager>().Object,
            agentManager.Object,
            store.Object,
            hubContext.Object,
            _shellSettings);

        // Act
        await handler.HandleAsync(new InteractionEvent { EventType = eventType, InteractionId = "interaction-1" }, TestContext.Current.CancellationToken);

        // Assert
        store.VerifyNoOtherCalls();
        hubContext.VerifyNoOtherCalls();
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
            CreatedUtc = new DateTime(2026, 7, 10, 13, 0, 0, DateTimeKind.Utc),
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 5, DateTimeKind.Utc),
        }.RestorePersistedStatus(InteractionStatus.Held);
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "Asterisk",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Outbound,
            IsOnHold = true,
            IsMuted = true,
            FromAddress = "+15550002000",
            ToAddress = "+15550001000",
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 5, DateTimeKind.Utc),
            AnsweredUtc = new DateTime(2026, 7, 10, 13, 0, 9, DateTimeKind.Utc),
        }.RestorePersistedState(VoiceCallState.OnHold);

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
        clients.Setup(value => value.Group(TenantSignalRGroupName.ForUser(_shellSettings.Name, "user-1"))).Returns(client.Object);
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        var handler = new ContactCenterSoftPhoneEventHandler(
            interactionManager.Object,
            callSessionManager.Object,
            agentManager.Object,
            store.Object,
            hubContext.Object,
            _shellSettings);

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
            CreatedUtc = new DateTime(2026, 7, 10, 13, 0, 0, DateTimeKind.Utc),
        }.RestorePersistedStatus(InteractionStatus.Ringing);
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "Asterisk",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Inbound,
            FromAddress = "+15550001000",
            ToAddress = "+15550002000",
            StartedUtc = new DateTime(2026, 7, 10, 13, 0, 0, DateTimeKind.Utc),
            AnsweredUtc = new DateTime(2026, 7, 10, 13, 0, 1, DateTimeKind.Utc),
        }.RestorePersistedState(VoiceCallState.Connected);

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
        clients.Setup(value => value.Group(TenantSignalRGroupName.ForUser(_shellSettings.Name, "user-1"))).Returns(client.Object);
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        var handler = new ContactCenterSoftPhoneEventHandler(
            interactionManager.Object,
            callSessionManager.Object,
            agentManager.Object,
            store.Object,
            hubContext.Object,
            _shellSettings);

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

    // Bug: on a call handed off from the AI assistant, the soft phone's number field showed the platform's own number.
    // The call session's addresses follow whichever provider leg reported last -- here the leg the platform dialed
    // from its own number -- so the projection took the tenant's caller id for the customer. The customer is the
    // interaction's customer address, whatever the session's legs say.
    [Theory]
    [InlineData(InteractionDirection.Inbound)]
    [InlineData(InteractionDirection.Outbound)]
    public async Task HandleAsync_PushesTheCustomerAsTheRemoteParty_NotThePlatformNumber(InteractionDirection direction)
    {
        // Arrange
        const string customer = "+15550001000";
        const string platform = "+15550009000";

        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "Telnyx",
            ProviderInteractionId = "call-1",
            CustomerAddress = customer,
            AgentId = "agent-1",
            Direction = direction,
            CreatedUtc = new DateTime(2026, 9, 24, 15, 43, 17, DateTimeKind.Utc),
        }.RestorePersistedStatus(InteractionStatus.Connected);
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            Direction = direction,
            FromAddress = direction == InteractionDirection.Inbound ? platform : customer,
            ToAddress = direction == InteractionDirection.Inbound ? customer : platform,
        }.RestorePersistedState(VoiceCallState.Connected);

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager.Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(manager => manager.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });

        TelephonyCall pushed = null;
        var client = new Mock<ITelephonyClient>();
        client.Setup(value => value.CallStateChanged(It.IsAny<TelephonyCall>()))
            .Callback<TelephonyCall>(call => pushed = call)
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients<ITelephonyClient>>();
        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        clients.Setup(value => value.Group(TenantSignalRGroupName.ForUser(_shellSettings.Name, "user-1"))).Returns(client.Object);
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        var handler = new ContactCenterSoftPhoneEventHandler(
            interactionManager.Object,
            callSessionManager.Object,
            agentManager.Object,
            new Mock<ITelephonyInteractionStore>().Object,
            hubContext.Object,
            _shellSettings);

        // Act
        await handler.HandleAsync(
            new InteractionEvent { EventType = ContactCenterConstants.Events.CallConnected, InteractionId = "interaction-1" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(pushed);

        if (direction == InteractionDirection.Inbound)
        {
            Assert.Equal(customer, pushed.From);
            Assert.Equal(platform, pushed.To);
        }
        else
        {
            Assert.Equal(platform, pushed.From);
            Assert.Equal(customer, pushed.To);
        }
    }
}
