using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderVoiceEventServiceTests
{
    [Fact]
    public async Task IngestAsync_WithMuteAndRecordingChanges_PublishesDetailedEvents()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderInteractionId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Inbound,
            Status = InteractionStatus.Connected,
            RecordingState = RecordingState.None,
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Inbound,
            State = ContactCenterCallState.Connected,
            IsMuted = false,
            RecordingState = RecordingState.None,
            IsConference = false,
            ParticipantCount = 1,
        };
        var publishedEvents = new List<InteractionEvent>();

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindByProviderInteractionIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager.Setup(manager => manager.FindByProviderCallIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var eventStore = new Mock<IInteractionEventStore>();
        eventStore.Setup(store => store.ExistsByIdempotencyKeyAsync("key-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var publisher = new Mock<IContactCenterEventPublisher>();
        publisher.Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => publishedEvents.Add(interactionEvent))
            .Returns(Task.CompletedTask);

        var presenceManager = new Mock<IAgentPresenceManager>();
        var voiceProviderResolver = new Mock<IContactCenterVoiceProviderResolver>();
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc));

        var service = new ProviderVoiceEventService(
            interactionManager.Object,
            callSessionManager.Object,
            voiceProviderResolver.Object,
            eventStore.Object,
            publisher.Object,
            presenceManager.Object,
            clock.Object,
            NullLogger<ProviderVoiceEventService>.Instance);

        // Act
        await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Connected,
            IdempotencyKey = "key-1",
            IsMuted = true,
            RecordingState = RecordingState.Recording,
            RecordingReference = "rec-1",
            IsConference = true,
            ParticipantCount = 3,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RecordingState.Recording, interaction.RecordingState);
        Assert.Equal("rec-1", interaction.RecordingReference);
        Assert.True(session.IsMuted);
        Assert.True(session.IsConference);
        Assert.Equal(3, session.ParticipantCount);
        Assert.Equal("rec-1", session.RecordingReference);
        Assert.Contains(publishedEvents, value => value.EventType == ContactCenterConstants.Events.CallSessionUpdated);
        Assert.Contains(publishedEvents, value => value.EventType == ContactCenterConstants.Events.CallMuted);
        Assert.Contains(publishedEvents, value => value.EventType == ContactCenterConstants.Events.RecordingStarted);
        Assert.Contains(publishedEvents, value => value.EventType == ContactCenterConstants.Events.CallConferenceChanged);
        Assert.Equal(
            publishedEvents.Count,
            publishedEvents.Select(value => value.IdempotencyKey).Distinct(StringComparer.Ordinal).Count());
        interactionManager.Verify(
            manager => manager.FindByProviderInteractionIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()),
            Times.Once);
        callSessionManager.Verify(
            manager => manager.FindByProviderCallIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_WhenCallResumesAndRecordingStops_PublishesResumeAndStopEvents()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderInteractionId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Inbound,
            Status = InteractionStatus.Held,
            RecordingState = RecordingState.Paused,
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Inbound,
            State = ContactCenterCallState.OnHold,
            IsMuted = true,
            RecordingState = RecordingState.Paused,
            IsConference = true,
            ParticipantCount = 3,
        };
        var publishedEvents = new List<InteractionEvent>();

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindByProviderInteractionIdAsync("call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager.Setup(manager => manager.FindByProviderCallIdAsync("call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var eventStore = new Mock<IInteractionEventStore>();
        eventStore.Setup(store => store.ExistsByIdempotencyKeyAsync("key-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var publisher = new Mock<IContactCenterEventPublisher>();
        publisher.Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => publishedEvents.Add(interactionEvent))
            .Returns(Task.CompletedTask);

        var presenceManager = new Mock<IAgentPresenceManager>();
        var voiceProviderResolver = new Mock<IContactCenterVoiceProviderResolver>();
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc));

        var service = new ProviderVoiceEventService(
            interactionManager.Object,
            callSessionManager.Object,
            voiceProviderResolver.Object,
            eventStore.Object,
            publisher.Object,
            presenceManager.Object,
            clock.Object,
            NullLogger<ProviderVoiceEventService>.Instance);

        // Act
        await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Connected,
            IdempotencyKey = "key-2",
            IsMuted = false,
            RecordingState = RecordingState.Stopped,
            IsConference = false,
            ParticipantCount = 2,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(publishedEvents, value => value.EventType == ContactCenterConstants.Events.CallResumed);
        Assert.Contains(publishedEvents, value => value.EventType == ContactCenterConstants.Events.CallUnmuted);
        Assert.Contains(publishedEvents, value => value.EventType == ContactCenterConstants.Events.RecordingStopped);
        Assert.Contains(publishedEvents, value => value.EventType == ContactCenterConstants.Events.CallConferenceChanged);
    }

    [Fact]
    public async Task IngestAsync_WithRealPublisher_PersistsEverySemanticEventWithAUniqueKey()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderInteractionId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Inbound,
            Status = InteractionStatus.Connected,
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Inbound,
            State = ContactCenterCallState.Connected,
        };
        var persistedEvents = new List<InteractionEvent>();
        var persistedKeys = new HashSet<string>(StringComparer.Ordinal);
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByProviderCallIdAsync("call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => persistedKeys.Contains(key));
        eventStore
            .Setup(store => store.CreateAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) =>
            {
                persistedEvents.Add(interactionEvent);

                if (!string.IsNullOrEmpty(interactionEvent.IdempotencyKey))
                {
                    persistedKeys.Add(interactionEvent.IdempotencyKey);
                }
            })
            .Returns(ValueTask.CompletedTask);
        var outbox = new Mock<IContactCenterOutbox>();
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc));
        var publisher = new DefaultContactCenterEventPublisher(
            eventStore.Object,
            outbox.Object,
            clock.Object,
            NullLogger<DefaultContactCenterEventPublisher>.Instance);
        var service = new ProviderVoiceEventService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            eventStore.Object,
            publisher,
            new Mock<IAgentPresenceManager>().Object,
            clock.Object,
            NullLogger<ProviderVoiceEventService>.Instance);
        var providerEvent = new ProviderVoiceEvent
        {
            ProviderCallId = "call-1",
            State = ContactCenterCallState.OnHold,
            IdempotencyKey = "provider-event-1",
        };

        // Act
        await service.IngestAsync(providerEvent, TestContext.Current.CancellationToken);
        await service.IngestAsync(providerEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Collection(
            persistedEvents,
            interactionEvent =>
            {
                Assert.Equal(ContactCenterConstants.Events.CallSessionUpdated, interactionEvent.EventType);
                Assert.Equal("provider-event-1", interactionEvent.IdempotencyKey);
            },
            interactionEvent =>
            {
                Assert.Equal(ContactCenterConstants.Events.CallHeld, interactionEvent.EventType);
                Assert.Equal($"provider-event-1:{ContactCenterConstants.Events.CallHeld}", interactionEvent.IdempotencyKey);
            });
        outbox.Verify(value => value.EnqueueAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        outbox.Verify(value => value.DispatchAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task IngestAsync_WhenEventIsOlderThanTerminalState_DoesNotReopenCall()
    {
        // Arrange
        var endedUtc = new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc);
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderInteractionId = "call-1",
            Status = InteractionStatus.Ended,
            EndedUtc = endedUtc,
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Ended,
            EndedUtc = endedUtc,
            LastProviderEventUtc = endedUtc,
        };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByProviderCallIdAsync("call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync("late-connected", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = new ProviderVoiceEventService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            eventStore.Object,
            publisher.Object,
            new Mock<IAgentPresenceManager>().Object,
            new Mock<IClock>().Object,
            NullLogger<ProviderVoiceEventService>.Instance);

        // Act
        await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Connected,
            IdempotencyKey = "late-connected",
            OccurredUtc = endedUtc.AddSeconds(-5),
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ContactCenterCallState.Ended, session.State);
        Assert.Equal(InteractionStatus.Ended, interaction.Status);
        callSessionManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        interactionManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        publisher.Verify(
            value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
