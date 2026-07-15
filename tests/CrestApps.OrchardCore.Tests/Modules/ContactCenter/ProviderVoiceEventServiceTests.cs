#nullable enable annotations

using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderVoiceEventServiceTests
{
    [Fact]
    public async Task IngestAsync_WhenProviderNameIsAnAlias_ResolvesCanonicalIdentityWithoutMutatingStoredProvider()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "Asterisk",
            ProviderInteractionId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Inbound,
            Status = InteractionStatus.Connected,
            AnsweredUtc = new DateTime(2026, 7, 10, 14, 59, 0, DateTimeKind.Utc),
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
            AnsweredUtc = interaction.AnsweredUtc,
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("Asterisk", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByProviderCallIdAsync("Asterisk", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync("ended-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var presenceManager = new Mock<IAgentPresenceManager>();
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc));

        var service = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            new Mock<IContactCenterEventPublisher>().Object,
            presenceManager.Object,
            new ProviderIdentityResolver([new TestProviderIdentityProvider(new ProviderIdentity("Asterisk", "Default Asterisk"))]),
            clock.Object,
            NullLogger<ProviderVoiceEventService>.Instance);

        // Act
        await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "Default Asterisk",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Ended,
            IdempotencyKey = "ended-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Asterisk", interaction.ProviderName);
        Assert.Equal("Asterisk", session.ProviderName);
        Assert.Equal(InteractionStatus.Ended, interaction.Status);
        Assert.Equal(ContactCenterCallState.Ended, session.State);
        Assert.Equal(new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc), interaction.WrapUpStartedUtc);
        presenceManager.Verify(
            manager => manager.StartWrapUpAsync("agent-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_WhenCallIdBelongsToAnotherActiveProvider_DoesNotCanonicalizeInteraction()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderInteractionId = "shared-call-id",
            Status = InteractionStatus.Connected,
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("ProviderB", "shared-call-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Interaction)null);
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("shared-call-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var telephonyProviderResolver = new Mock<ITelephonyProviderResolver>();
        telephonyProviderResolver
            .Setup(resolver => resolver.GetAsync("ProviderA"))
            .ReturnsAsync(new Mock<ITelephonyProvider>().Object);

        var callSessionManager = new Mock<ICallSessionManager>();
        var service = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            telephonyProviderResolver.Object,
            new Mock<IInteractionEventStore>().Object,
            new Mock<IContactCenterEventPublisher>().Object,
            new Mock<IAgentPresenceManager>().Object,
            new ProviderIdentityResolver([]),
            new Mock<IClock>().Object,
            NullLogger<ProviderVoiceEventService>.Instance);

        // Act
        var result = await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "ProviderB",
            ProviderCallId = "shared-call-id",
            State = ContactCenterCallState.Ended,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
        Assert.Equal("ProviderA", interaction.ProviderName);
        Assert.Equal(InteractionStatus.Connected, interaction.Status);
        callSessionManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

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
        var providerCommandStateService = new Mock<IProviderCommandStateService>(MockBehavior.Strict);
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>(MockBehavior.Strict);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc));

        var service = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            voiceProviderResolver.Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            publisher.Object,
            presenceManager.Object,
            new ProviderIdentityResolver([]),
            clock.Object,
            NullLogger<ProviderVoiceEventService>.Instance,
            providerCommandStateService.Object,
            scopeExecutor.Object);

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
        providerCommandStateService.Verify(
            value => value.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()),
            Times.Never);
        scopeExecutor.Verify(
            value => value.ScheduleAfterCommit<IProviderCommandProcessor>(
                It.IsAny<Func<IProviderCommandProcessor, Task>>()),
            Times.Never);
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

        var service = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            voiceProviderResolver.Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            publisher.Object,
            presenceManager.Object,
            new ProviderIdentityResolver([]),
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
            new TestContactCenterScopeExecutor(new ServiceCollection().BuildServiceProvider()),
            clock.Object,
            NullLogger<DefaultContactCenterEventPublisher>.Instance);
        var service = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            publisher,
            new Mock<IAgentPresenceManager>().Object,
            new ProviderIdentityResolver([]),
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
                Assert.Equal(
                    ContactCenterClaimKeys.BuildProviderDomainEventIdempotencyKey(
                        "provider-event-1",
                        ContactCenterConstants.Events.CallHeld),
                    interactionEvent.IdempotencyKey);
            });
        outbox.Verify(value => value.EnqueueAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        outbox.Verify(value => value.DispatchAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
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
        var service = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            publisher.Object,
            new Mock<IAgentPresenceManager>().Object,
            new ProviderIdentityResolver([]),
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

    [Fact]
    public async Task IngestAsync_WhenOutOfOrderEventHasEqualTimestamp_DoesNotRegressConnectedCall()
    {
        // Arrange
        var occurredUtc = new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc);
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderInteractionId = "call-1",
            Status = InteractionStatus.Connected,
            AnsweredUtc = occurredUtc,
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Connected,
            AnsweredUtc = occurredUtc,
            LastProviderEventUtc = occurredUtc,
        };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByProviderCallIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync("ringing-older-sequence", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var service = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            new Mock<IContactCenterEventPublisher>().Object,
            new Mock<IAgentPresenceManager>().Object,
            new ProviderIdentityResolver([]),
            new Mock<IClock>().Object,
            NullLogger<ProviderVoiceEventService>.Instance);

        // Act
        await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Ringing,
            IdempotencyKey = "ringing-older-sequence",
            OccurredUtc = occurredUtc,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ContactCenterCallState.Connected, session.State);
        Assert.Equal(InteractionStatus.Connected, interaction.Status);
        callSessionManager.Verify(
            manager => manager.UpdateAsync(
                It.IsAny<CallSession>(),
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IngestAsync_WhenSequenceIsAtOrBelowHighWater_IgnoresStaleEvent()
    {
        // Arrange
        var connectedUtc = new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc);
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderInteractionId = "call-1",
            Status = InteractionStatus.Connected,
            AnsweredUtc = connectedUtc,
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Connected,
            AnsweredUtc = connectedUtc,
            LastProviderEventUtc = connectedUtc.AddSeconds(-5),
            HighWaterSequence = 5,
        };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByProviderCallIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync("late-ringing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var service = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            new Mock<IContactCenterEventPublisher>().Object,
            new Mock<IAgentPresenceManager>().Object,
            new ProviderIdentityResolver([]),
            new Mock<IClock>().Object,
            NullLogger<ProviderVoiceEventService>.Instance);

        // Act
        await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Ringing,
            IdempotencyKey = "late-ringing",
            OccurredUtc = connectedUtc.AddSeconds(10),
            SequenceNumber = 4,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ContactCenterCallState.Connected, session.State);
        Assert.Equal(InteractionStatus.Connected, interaction.Status);
        Assert.Equal(5, session.HighWaterSequence);
        callSessionManager.Verify(
            manager => manager.UpdateAsync(
                It.IsAny<CallSession>(),
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IngestAsync_WhenSequenceAdvances_UpdatesHighWaterMark()
    {
        // Arrange
        var startedUtc = new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc);
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderInteractionId = "call-1",
            Status = InteractionStatus.Ringing,
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Ringing,
            LastProviderEventUtc = startedUtc,
            HighWaterSequence = 5,
        };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByProviderCallIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync("connected", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(startedUtc.AddSeconds(3));
        var service = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            new Mock<IContactCenterEventPublisher>().Object,
            new Mock<IAgentPresenceManager>().Object,
            new ProviderIdentityResolver([]),
            clock.Object,
            NullLogger<ProviderVoiceEventService>.Instance);

        // Act
        await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Connected,
            IdempotencyKey = "connected",
            OccurredUtc = startedUtc.AddSeconds(3),
            SequenceNumber = 6,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ContactCenterCallState.Connected, session.State);
        Assert.Equal(6, session.HighWaterSequence);
    }

    [Fact]
    public async Task IngestAsync_WhenSessionIsAlreadyTerminal_DoesNotRepublishLaterTerminalEvent()
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
            .Setup(store => store.ExistsByIdempotencyKeyAsync("later-terminal", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            publisher.Object,
            new Mock<IAgentPresenceManager>().Object,
            new ProviderIdentityResolver([]),
            new Mock<IClock>().Object,
            NullLogger<ProviderVoiceEventService>.Instance);

        // Act
        var result = await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Ended,
            IdempotencyKey = "later-terminal",
            OccurredUtc = endedUtc.AddSeconds(1),
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(session, result);
        callSessionManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        publisher.Verify(
            value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IngestAsync_WhenEventIsDuplicate_ReturnsExistingSession()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderInteractionId = "call-1",
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
        };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByProviderCallIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var eventStore = new Mock<IInteractionEventStore>();
        var scopedIdempotencyKey = ContactCenterClaimKeys.BuildProviderEventIdempotencyKey("ProviderA", "duplicate");
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync(scopedIdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            new Mock<IContactCenterEventPublisher>().Object,
            new Mock<IAgentPresenceManager>().Object,
            new ProviderIdentityResolver([]),
            new Mock<IClock>().Object,
            NullLogger<ProviderVoiceEventService>.Instance);

        // Act
        var result = await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Ended,
            IdempotencyKey = "duplicate",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(session, result);
    }

    [Fact]
    public async Task IngestAsync_WhenLegacyRawEventExistsForInteraction_DoesNotReplayAfterKeyUpgrade()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderInteractionId = "call-1",
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
        };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByProviderCallIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        eventStore
            .Setup(store => store.ListByInteractionAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new InteractionEvent
                {
                    InteractionId = "interaction-1",
                    IdempotencyKey = "legacy-delivery",
                },
            ]);
        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            publisher.Object,
            new Mock<IAgentPresenceManager>().Object,
            new ProviderIdentityResolver([]),
            new Mock<IClock>().Object,
            NullLogger<ProviderVoiceEventService>.Instance);

        // Act
        var result = await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Ended,
            IdempotencyKey = "legacy-delivery",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(session, result);
        publisher.Verify(
            value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IngestAsync_WhenTwoProvidersShareARawIdempotencyKey_ScopesKeysAndDoesNotCollide()
    {
        // Arrange
        var connectedUtc = new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc);
        var asteriskInteraction = new Interaction
        {
            ItemId = "interaction-asterisk",
            ProviderName = "Asterisk",
            ProviderInteractionId = "call-asterisk",
            Status = InteractionStatus.Connected,
            AnsweredUtc = connectedUtc,
        };
        var dialPadInteraction = new Interaction
        {
            ItemId = "interaction-dialpad",
            ProviderName = "DialPad",
            ProviderInteractionId = "call-dialpad",
            Status = InteractionStatus.Connected,
            AnsweredUtc = connectedUtc,
        };
        var asteriskSession = new CallSession
        {
            ItemId = "session-asterisk",
            InteractionId = "interaction-asterisk",
            ProviderName = "Asterisk",
            ProviderCallId = "call-asterisk",
            State = ContactCenterCallState.Connected,
            AnsweredUtc = connectedUtc,
        };
        var dialPadSession = new CallSession
        {
            ItemId = "session-dialpad",
            InteractionId = "interaction-dialpad",
            ProviderName = "DialPad",
            ProviderCallId = "call-dialpad",
            State = ContactCenterCallState.Connected,
            AnsweredUtc = connectedUtc,
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("Asterisk", "call-asterisk", It.IsAny<CancellationToken>()))
            .ReturnsAsync(asteriskInteraction);
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("DialPad", "call-dialpad", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dialPadInteraction);
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByProviderCallIdAsync("Asterisk", "call-asterisk", It.IsAny<CancellationToken>()))
            .ReturnsAsync(asteriskSession);
        callSessionManager
            .Setup(manager => manager.FindByProviderCallIdAsync("DialPad", "call-dialpad", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dialPadSession);

        // The event store simulates real database-backed idempotency: a key that was already published is
        // reported as existing on the next lookup.
        var persistedKeys = new HashSet<string>(StringComparer.Ordinal);
        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => persistedKeys.Contains(key));
        var publishedEvents = new List<InteractionEvent>();
        var publisher = new Mock<IContactCenterEventPublisher>();
        publisher
            .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) =>
            {
                publishedEvents.Add(interactionEvent);

                if (!string.IsNullOrEmpty(interactionEvent.IdempotencyKey))
                {
                    persistedKeys.Add(interactionEvent.IdempotencyKey);
                }
            })
            .Returns(Task.CompletedTask);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(connectedUtc.AddSeconds(1));
        var service = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            publisher.Object,
            new Mock<IAgentPresenceManager>().Object,
            new ProviderIdentityResolver([new TestProviderIdentityProvider(new ProviderIdentity("Asterisk", "Default Asterisk"))]),
            clock.Object,
            NullLogger<ProviderVoiceEventService>.Instance);

        // Act
        // Both providers deliver the same raw delivery identifier, but for different calls. An alias
        // ("Default Asterisk") is used for the first delivery to prove canonicalization runs before keying.
        await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "Default Asterisk",
            ProviderCallId = "call-asterisk",
            State = ContactCenterCallState.Ended,
            IdempotencyKey = "delivery-42",
            OccurredUtc = connectedUtc.AddSeconds(1),
        }, TestContext.Current.CancellationToken);
        await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "DialPad",
            ProviderCallId = "call-dialpad",
            State = ContactCenterCallState.Ended,
            IdempotencyKey = "delivery-42",
            OccurredUtc = connectedUtc.AddSeconds(1),
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(
            publishedEvents,
            value => value.IdempotencyKey == ContactCenterClaimKeys.BuildProviderEventIdempotencyKey("Asterisk", "delivery-42"));
        Assert.Contains(
            publishedEvents,
            value => value.IdempotencyKey == ContactCenterClaimKeys.BuildProviderEventIdempotencyKey("DialPad", "delivery-42"));
        Assert.Equal(ContactCenterCallState.Ended, asteriskSession.State);
        Assert.Equal(ContactCenterCallState.Ended, dialPadSession.State);
    }

    [Fact]
    public async Task IngestAsync_WhenFirstObservedEventForNewSessionIsTerminal_PublishesCallEnded()
    {
        // Arrange
        // Reconciliation discovers that a queued (never answered) call no longer exists on the provider and
        // emits a terminal event before any session was ever created. The service must still record a real
        // non-terminal -> terminal transition so CallEnded is published and the offer cleanup runs.
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderInteractionId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Inbound,
            Status = InteractionStatus.Ringing,
        };
        var publishedEvents = new List<InteractionEvent>();

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByProviderCallIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CallSession)null);
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CallSession)null);
        callSessionManager
            .Setup(manager => manager.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallSession());

        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var publisher = new Mock<IContactCenterEventPublisher>();
        publisher
            .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => publishedEvents.Add(interactionEvent))
            .Returns(Task.CompletedTask);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc));

        var service = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            publisher.Object,
            new Mock<IAgentPresenceManager>().Object,
            new ProviderIdentityResolver([]),
            clock.Object,
            NullLogger<ProviderVoiceEventService>.Instance);

        // Act
        await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Ended,
            IdempotencyKey = "ended-new-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Ended, interaction.Status);
        Assert.Contains(publishedEvents, value => value.EventType == ContactCenterConstants.Events.CallEnded);
    }

    [Fact]
    public async Task IngestAsync_WhenOutboundConnectedEventRequiresAgentBridge_RegistersDurableAnswerCommand()
    {
        // Arrange
        var now = new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc);
        var order = new List<string>();
        var publishedEvents = new List<InteractionEvent>();
        ProviderCommandRegistration? capturedRegistration = null;
        Func<IProviderCommandProcessor, Task>? scheduledDispatch = null;

        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "ProviderA",
            ProviderInteractionId = "call-1",
            AgentId = "agent-1",
            QueueId = "queue-1",
            Direction = InteractionDirection.Outbound,
            Status = InteractionStatus.Ringing,
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            QueueId = "queue-1",
            Direction = InteractionDirection.Outbound,
            State = ContactCenterCallState.Ringing,
        };

        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        interactionManager
            .Setup(manager => manager.UpdateAsync(
                interaction,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("interaction-update"))
            .Returns(ValueTask.CompletedTask);

        var callSessionManager = new Mock<ICallSessionManager>(MockBehavior.Strict);
        callSessionManager
            .Setup(manager => manager.FindByProviderCallIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        callSessionManager
            .Setup(manager => manager.UpdateAsync(
                session,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("session-update"))
            .Returns(ValueTask.CompletedTask);

        var eventStore = new Mock<IInteractionEventStore>(MockBehavior.Strict);
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync(
                ContactCenterClaimKeys.BuildProviderEventIdempotencyKey("ProviderA", "connected-1"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        eventStore
            .Setup(store => store.ListByInteractionAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var publisher = new Mock<IContactCenterEventPublisher>(MockBehavior.Strict);
        publisher
            .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) =>
            {
                publishedEvents.Add(interactionEvent);
                order.Add($"publish:{interactionEvent.EventType}");
            })
            .Returns(Task.CompletedTask);

        var voiceProvider = new Mock<IContactCenterVoiceProvider>(MockBehavior.Strict);
        voiceProvider
            .SetupGet(value => value.Capabilities)
            .Returns(ContactCenterVoiceProviderCapabilities.AgentConnect);
        voiceProvider
            .SetupGet(value => value.DeliveryModel)
            .Returns(VoiceProviderDeliveryModel.ServerSideAcd);

        var voiceProviderResolver = new Mock<IContactCenterVoiceProviderResolver>(MockBehavior.Strict);
        voiceProviderResolver
            .Setup(resolver => resolver.Get("ProviderA"))
            .Returns(voiceProvider.Object);

        var providerCommandStateService = new Mock<IProviderCommandStateService>(MockBehavior.Strict);
        providerCommandStateService
            .Setup(service => service.RegisterAsync(
                It.IsAny<ProviderCommandRegistration>(),
                It.IsAny<CancellationToken>()))
            .Callback<ProviderCommandRegistration, CancellationToken>((registration, _) =>
            {
                capturedRegistration = registration;
                order.Add("register");
            })
            .ReturnsAsync((ProviderCommandRegistration registration, CancellationToken _) => new ProviderCommand
            {
                CommandId = registration.CommandId,
                Status = ProviderCommandStatus.Pending,
            });

        var scopeExecutor = new Mock<IContactCenterScopeExecutor>(MockBehavior.Strict);
        scopeExecutor
            .Setup(executor => executor.ScheduleAfterCommit<IProviderCommandProcessor>(
                It.IsAny<Func<IProviderCommandProcessor, Task>>()))
            .Callback<Func<IProviderCommandProcessor, Task>>(operation =>
            {
                scheduledDispatch = operation;
                order.Add("schedule");
            })
            .Returns(true);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(now);

        var service = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            voiceProviderResolver.Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            publisher.Object,
            new Mock<IAgentPresenceManager>().Object,
            new ProviderIdentityResolver([]),
            clock.Object,
            NullLogger<ProviderVoiceEventService>.Instance,
            providerCommandStateService.Object,
            scopeExecutor.Object);

        // Act
        await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Connected,
            IdempotencyKey = "connected-1",
        }, TestContext.Current.CancellationToken);

        var processor = new Mock<IProviderCommandProcessor>(MockBehavior.Strict);
        Assert.NotNull(capturedRegistration);
        var registration = capturedRegistration;
        var commandId = registration.CommandId;
        Assert.False(string.IsNullOrWhiteSpace(commandId));
        processor
            .Setup(value => value.DispatchAsync(commandId, CancellationToken.None))
            .ReturnsAsync(new ProviderCommand
            {
                CommandId = commandId,
                Status = ProviderCommandStatus.Sent,
            });

        Assert.NotNull(scheduledDispatch);
        var dispatch = scheduledDispatch;

        await dispatch(processor.Object);

        // Assert
        Assert.Equal(
            [
                "session-update",
                "interaction-update",
                $"publish:{ContactCenterConstants.Events.CallSessionUpdated}",
                $"publish:{ContactCenterConstants.Events.CallConnected}",
                "session-update",
                "register",
                "schedule",
            ],
            order);
        Assert.Equal(ContactCenterCallState.Connected, session.State);
        Assert.Equal(InteractionStatus.Connected, interaction.Status);
        Assert.Equal(now, session.AnsweredUtc);
        Assert.Equal(now, interaction.AnsweredUtc);
        Assert.Equal(commandId, session.Metadata[ContactCenterConstants.CommandMetadata.CommandId]);
        Assert.Equal(commandId, registration.CommandId);
        Assert.Equal(ProviderCommandType.Answer, registration.CommandType);
        Assert.Equal("ProviderA", registration.ProviderName);
        Assert.Equal("activity-1", registration.ActivityItemId);
        Assert.Equal("interaction-1", registration.InteractionId);
        Assert.False(string.IsNullOrWhiteSpace(registration.RequestPayload));

        var request = JsonSerializer.Deserialize<ProviderAnswerCommandRequest>(registration.RequestPayload)!;
        Assert.Equal("activity-1", request.ActivityId);
        Assert.Equal("interaction-1", request.InteractionId);
        Assert.Equal("call-1", request.ProviderCallId);
        Assert.Equal("agent-1", request.AgentId);
        Assert.Equal("queue-1", request.QueueId);

        Assert.Collection(
            publishedEvents,
            value => Assert.Equal(ContactCenterConstants.Events.CallSessionUpdated, value.EventType),
            value => Assert.Equal(ContactCenterConstants.Events.CallConnected, value.EventType));
        voiceProvider.Verify(
            value => value.ConnectToAgentAsync(It.IsAny<ContactCenterConnectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        callSessionManager.Verify(
            manager => manager.UpdateAsync(
                session,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        interactionManager.Verify(
            manager => manager.UpdateAsync(
                interaction,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        scopeExecutor.Verify(
            executor => executor.ScheduleAfterCommit<IProviderCommandProcessor>(
                It.IsAny<Func<IProviderCommandProcessor, Task>>()),
            Times.Once);
        processor.Verify(
            value => value.DispatchAsync(commandId, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_WhenOutboundConnectedEventReplays_ReusesStoredCommandId()
    {
        // Arrange
        var order = new List<string>();
        var publishedEvents = new List<InteractionEvent>();
        var commandIds = new List<string>();
        var scheduledDispatches = new List<Func<IProviderCommandProcessor, Task>>();

        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "ProviderA",
            ProviderInteractionId = "call-1",
            AgentId = "agent-1",
            QueueId = "queue-1",
            Direction = InteractionDirection.Outbound,
            Status = InteractionStatus.Ringing,
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            QueueId = "queue-1",
            Direction = InteractionDirection.Outbound,
            State = ContactCenterCallState.Ringing,
        };

        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        interactionManager
            .Setup(manager => manager.UpdateAsync(
                interaction,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("interaction-update"))
            .Returns(ValueTask.CompletedTask);

        var callSessionManager = new Mock<ICallSessionManager>(MockBehavior.Strict);
        callSessionManager
            .Setup(manager => manager.FindByProviderCallIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        callSessionManager
            .Setup(manager => manager.UpdateAsync(
                session,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("session-update"))
            .Returns(ValueTask.CompletedTask);

        var eventStore = new Mock<IInteractionEventStore>(MockBehavior.Strict);
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        eventStore
            .Setup(store => store.ListByInteractionAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var publisher = new Mock<IContactCenterEventPublisher>(MockBehavior.Strict);
        publisher
            .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) =>
            {
                publishedEvents.Add(interactionEvent);
                order.Add($"publish:{interactionEvent.EventType}");
            })
            .Returns(Task.CompletedTask);

        var voiceProvider = new Mock<IContactCenterVoiceProvider>(MockBehavior.Strict);
        voiceProvider
            .SetupGet(value => value.Capabilities)
            .Returns(ContactCenterVoiceProviderCapabilities.AgentConnect);
        voiceProvider
            .SetupGet(value => value.DeliveryModel)
            .Returns(VoiceProviderDeliveryModel.ServerSideAcd);

        var voiceProviderResolver = new Mock<IContactCenterVoiceProviderResolver>(MockBehavior.Strict);
        voiceProviderResolver
            .Setup(resolver => resolver.Get("ProviderA"))
            .Returns(voiceProvider.Object);

        var providerCommandStateService = new Mock<IProviderCommandStateService>(MockBehavior.Strict);
        providerCommandStateService
            .Setup(service => service.RegisterAsync(
                It.IsAny<ProviderCommandRegistration>(),
                It.IsAny<CancellationToken>()))
            .Callback<ProviderCommandRegistration, CancellationToken>((registration, _) =>
            {
                commandIds.Add(registration.CommandId);
                order.Add("register");
            })
            .ReturnsAsync((ProviderCommandRegistration registration, CancellationToken _) => new ProviderCommand
            {
                CommandId = registration.CommandId,
                Status = ProviderCommandStatus.Pending,
            });

        var scopeExecutor = new Mock<IContactCenterScopeExecutor>(MockBehavior.Strict);
        scopeExecutor
            .Setup(executor => executor.ScheduleAfterCommit<IProviderCommandProcessor>(
                It.IsAny<Func<IProviderCommandProcessor, Task>>()))
            .Callback<Func<IProviderCommandProcessor, Task>>(operation =>
            {
                scheduledDispatches.Add(operation);
                order.Add("schedule");
            })
            .Returns(true);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc));

        var service = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            voiceProviderResolver.Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            publisher.Object,
            new Mock<IAgentPresenceManager>().Object,
            new ProviderIdentityResolver([]),
            clock.Object,
            NullLogger<ProviderVoiceEventService>.Instance,
            providerCommandStateService.Object,
            scopeExecutor.Object);

        // Act
        await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Connected,
            IdempotencyKey = "connected-1",
        }, TestContext.Current.CancellationToken);

        await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Connected,
            IdempotencyKey = "connected-2",
        }, TestContext.Current.CancellationToken);

        var processor = new Mock<IProviderCommandProcessor>(MockBehavior.Strict);
        Assert.NotNull(commandIds[0]);
        var storedCommandId = commandIds[0];
        Assert.False(string.IsNullOrWhiteSpace(storedCommandId));
        processor
            .Setup(value => value.DispatchAsync(storedCommandId, CancellationToken.None))
            .ReturnsAsync(new ProviderCommand
            {
                CommandId = storedCommandId,
                Status = ProviderCommandStatus.Sent,
            });

        foreach (var dispatch in scheduledDispatches)
        {
            await dispatch(processor.Object);
        }

        // Assert
        Assert.Equal(2, commandIds.Count);
        Assert.All(commandIds, value => Assert.Equal(storedCommandId, value));
        Assert.Equal(storedCommandId, session.Metadata[ContactCenterConstants.CommandMetadata.CommandId]);
        Assert.Equal(2, scheduledDispatches.Count);
        Assert.Contains(
            publishedEvents,
            value => value.EventType == ContactCenterConstants.Events.CallConnected);
        voiceProvider.Verify(
            value => value.ConnectToAgentAsync(It.IsAny<ContactCenterConnectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        processor.Verify(
            value => value.DispatchAsync(storedCommandId, CancellationToken.None),
            Times.Exactly(2));
        scopeExecutor.Verify(
            executor => executor.ScheduleAfterCommit<IProviderCommandProcessor>(
                It.IsAny<Func<IProviderCommandProcessor, Task>>()),
            Times.Exactly(2));
    }

    private static ProviderVoiceEventService CreateService(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        ITelephonyProviderResolver telephonyProviderResolver,
        IInteractionEventStore eventStore,
        IContactCenterEventPublisher publisher,
        IAgentPresenceManager presenceManager,
        IProviderIdentityResolver providerIdentityResolver,
        IClock clock,
        ILogger<ProviderVoiceEventService> logger,
        IProviderCommandStateService? providerCommandStateService = null,
        IContactCenterScopeExecutor? scopeExecutor = null)
    {
        providerCommandStateService ??= new Mock<IProviderCommandStateService>(MockBehavior.Strict).Object;
        scopeExecutor ??= new Mock<IContactCenterScopeExecutor>(MockBehavior.Strict).Object;

        return new ProviderVoiceEventService(
            interactionManager,
            callSessionManager,
            voiceProviderResolver,
            telephonyProviderResolver,
            eventStore,
            publisher,
            presenceManager,
            providerIdentityResolver,
            providerCommandStateService,
            scopeExecutor,
            clock,
            logger);
    }
}
