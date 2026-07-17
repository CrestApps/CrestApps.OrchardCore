#nullable enable annotations

using System.Collections.Concurrent;
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
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderVoiceEventServiceTests
{
    [Fact]
    public async Task IngestAsync_WhenProviderCallLockIsUnavailable_ThrowsBeforeReadingState()
    {
        // Arrange
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(service => service.TryAcquireLockAsync(
                It.Is<string>(key =>
                    key.StartsWith("ContactCenterProviderVoiceEvent:", StringComparison.Ordinal) &&
                    !key.Contains("call-1", StringComparison.Ordinal)),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, false));
        var service = CreateService(
            interactionManager.Object,
            new Mock<ICallSessionManager>(MockBehavior.Strict).Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            new Mock<ITelephonyProviderResolver>().Object,
            new Mock<IInteractionEventStore>(MockBehavior.Strict).Object,
            new Mock<IContactCenterEventPublisher>(MockBehavior.Strict).Object,
            new Mock<IAgentPresenceManager>(MockBehavior.Strict).Object,
            new ProviderIdentityResolver([]),
            new Mock<IClock>().Object,
            NullLogger<ProviderVoiceEventService>.Instance,
            session: new Mock<ISession>(MockBehavior.Strict).Object,
            distributedLock: distributedLock.Object);

        // Act
        var exception = await Assert.ThrowsAsync<TimeoutException>(() => service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Ringing,
            IdempotencyKey = "event-1",
        }, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("provider call", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IngestAsync_WhenEventIsHandled_CommitsBeforeReleasingProviderCallLock()
    {
        // Arrange
        var endedUtc = new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc);
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderInteractionId = "call-1",
            Status = InteractionStatus.Ended,
            EndedUtc = endedUtc,
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Ended,
            EndedUtc = endedUtc,
            LastProviderEventUtc = endedUtc,
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
        var saveCompleted = false;
        var yesSqlSession = new Mock<ISession>();
        yesSqlSession
            .Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => saveCompleted = true)
            .Returns(Task.CompletedTask);
        var locker = new TestLocker(() => Assert.True(saveCompleted));
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(service => service.TryAcquireLockAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan?>()))
            .ReturnsAsync((locker, true));
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
            NullLogger<ProviderVoiceEventService>.Instance,
            session: yesSqlSession.Object,
            distributedLock: distributedLock.Object);

        // Act
        var result = await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Connected,
            IdempotencyKey = "late-connected",
            OccurredUtc = endedUtc.AddSeconds(-1),
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(session, result);
        Assert.True(locker.IsDisposed);
        yesSqlSession.Verify(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestAsync_WhenTwoNodesReceiveSameEvent_SerializesAndPublishesOnce()
    {
        // Arrange
        var ringingUtc = new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc);
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
            LastProviderEventUtc = ringingUtc,
        };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByProviderCallIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var eventPublished = 0;
        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Volatile.Read(ref eventPublished) != 0);
        var publisher = new Mock<IContactCenterEventPublisher>();
        publisher
            .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Exchange(ref eventPublished, 1))
            .Returns(Task.CompletedTask);
        var distributedLock = new TestDistributedLock();
        var nodeA = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            publisher.Object,
            new Mock<IAgentPresenceManager>().Object,
            new ProviderIdentityResolver([]),
            new Mock<IClock>().Object,
            NullLogger<ProviderVoiceEventService>.Instance,
            session: new Mock<ISession>().Object,
            distributedLock: distributedLock);
        var nodeB = CreateService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            publisher.Object,
            new Mock<IAgentPresenceManager>().Object,
            new ProviderIdentityResolver([]),
            new Mock<IClock>().Object,
            NullLogger<ProviderVoiceEventService>.Instance,
            session: new Mock<ISession>().Object,
            distributedLock: distributedLock);

        ProviderVoiceEvent CreateEvent()
        {
            return new ProviderVoiceEvent
            {
                ProviderName = "ProviderA",
                ProviderCallId = "call-1",
                State = ContactCenterCallState.Connected,
                IdempotencyKey = "connected-event",
                OccurredUtc = ringingUtc.AddSeconds(1),
            };
        }

        // Act
        var results = await Task.WhenAll(
            nodeA.IngestAsync(CreateEvent(), TestContext.Current.CancellationToken),
            nodeB.IngestAsync(CreateEvent(), TestContext.Current.CancellationToken));

        // Assert
        Assert.All(results, result => Assert.Same(session, result));
        Assert.Equal(ContactCenterCallState.Connected, session.State);
        callSessionManager.Verify(
            manager => manager.UpdateAsync(
                It.IsAny<CallSession>(),
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        publisher.Verify(
            value => value.PublishAsync(
                It.Is<InteractionEvent>(interactionEvent =>
                    interactionEvent.EventType == ContactCenterConstants.Events.CallConnected),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

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
    public async Task IngestAsync_WhenAnswerClassificationIsPresent_PersistsClassificationToSessionAndInteractionMetadata()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderInteractionId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Outbound,
            Status = InteractionStatus.Ringing,
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Outbound,
            State = ContactCenterCallState.Dialing,
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
            .Setup(store => store.ExistsByIdempotencyKeyAsync("amd-machine-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var publisher = new Mock<IContactCenterEventPublisher>();
        publisher
            .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
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
            IdempotencyKey = "amd-machine-1",
            AnswerClassification = AnswerClassification.Machine,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(session.Metadata.ContainsKey(ContactCenterConstants.TelephonyMetadata.AnswerClassification));
        Assert.Equal("Machine", session.Metadata[ContactCenterConstants.TelephonyMetadata.AnswerClassification]);
        Assert.True(interaction.TechnicalMetadata.ContainsKey(ContactCenterConstants.TelephonyMetadata.AnswerClassification));
        Assert.Equal("Machine", interaction.TechnicalMetadata[ContactCenterConstants.TelephonyMetadata.AnswerClassification]);
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
    public async Task IngestAsync_WhenTimestamplessLateRingingArrivesAfterConnected_DoesNotRegressCall()
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
            LastProviderEventUtc = connectedUtc,
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
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(connectedUtc.AddMinutes(1));
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
            clock.Object,
            NullLogger<ProviderVoiceEventService>.Instance);

        // Act
        await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Ringing,
            IdempotencyKey = "late-ringing",
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
        interactionManager.Verify(
            manager => manager.UpdateAsync(
                It.IsAny<Interaction>(),
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        publisher.Verify(
            value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IngestAsync_WhenHighWaterExistsAndIncomingEventHasNoSequence_DoesNotRegressCall()
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
            LastProviderEventUtc = connectedUtc,
            HighWaterSequence = 6,
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
            .Setup(store => store.ExistsByIdempotencyKeyAsync("unsequenced-ringing", It.IsAny<CancellationToken>()))
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
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Ringing,
            IdempotencyKey = "unsequenced-ringing",
            OccurredUtc = connectedUtc.AddMinutes(1),
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ContactCenterCallState.Connected, session.State);
        Assert.Equal(InteractionStatus.Connected, interaction.Status);
        Assert.Equal(6, session.HighWaterSequence);
        callSessionManager.Verify(
            manager => manager.UpdateAsync(
                It.IsAny<CallSession>(),
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        interactionManager.Verify(
            manager => manager.UpdateAsync(
                It.IsAny<Interaction>(),
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        publisher.Verify(
            value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()),
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
    public async Task IngestAsync_WhenDuplicateCanonicalizesInteraction_CommitsBeforeReleasingProviderCallLock()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "LegacyProvider",
            ProviderInteractionId = "call-1",
        };
        var callSession = new CallSession
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
            .ReturnsAsync(callSession);
        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync(
                ContactCenterClaimKeys.BuildProviderEventIdempotencyKey("ProviderA", "duplicate"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var saveCompleted = false;
        var yesSqlSession = new Mock<ISession>();
        yesSqlSession
            .Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => saveCompleted = true)
            .Returns(Task.CompletedTask);
        var locker = new TestLocker(() => Assert.True(saveCompleted));
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(service => service.TryAcquireLockAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan?>()))
            .ReturnsAsync((locker, true));
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
            NullLogger<ProviderVoiceEventService>.Instance,
            session: yesSqlSession.Object,
            distributedLock: distributedLock.Object);

        // Act
        var result = await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = ContactCenterCallState.Ended,
            IdempotencyKey = "duplicate",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(callSession, result);
        Assert.Equal("ProviderA", interaction.ProviderName);
        Assert.True(locker.IsDisposed);
        yesSqlSession.Verify(
            value => value.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
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
        var callControlProvider = voiceProvider.As<IContactCenterVoiceCallControlProvider>();
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
        callControlProvider.Verify(
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
        var callControlProvider = voiceProvider.As<IContactCenterVoiceCallControlProvider>();
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
        callControlProvider.Verify(
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

    [Fact]
    public async Task IngestAsync_WhenDialCreatedSessionHasBlankProviderCallId_ReconcilesToEventProviderCallId()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderInteractionId = "call-outbound-1",
            Direction = InteractionDirection.Outbound,
            Status = InteractionStatus.Ringing,
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderCallId = null,
            Direction = InteractionDirection.Outbound,
            State = ContactCenterCallState.Planned,
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(m => m.FindByProviderInteractionIdAsync("ProviderA", "call-outbound-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        interactionManager
            .Setup(m => m.UpdateAsync(interaction, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(m => m.FindByProviderCallIdAsync("ProviderA", "call-outbound-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CallSession)null);
        callSessionManager
            .Setup(m => m.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        callSessionManager
            .Setup(m => m.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(s => s.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc));

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
        var ingestResult = await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "ProviderA",
            ProviderCallId = "call-outbound-1",
            State = ContactCenterCallState.Ringing,
            IdempotencyKey = "ringing-1",
        }, TestContext.Current.CancellationToken);

        // Assert — reconciliation must populate the blank ProviderCallId
        Assert.Equal("call-outbound-1", session.ProviderCallId);
        Assert.NotNull(ingestResult);
        callSessionManager.Verify(
            m => m.UpdateAsync(session, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task IngestAsync_WhenSessionFoundByInteractionIdAlreadyHasProviderCallId_DoesNotOverwriteExistingId()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderInteractionId = "call-new",
            Direction = InteractionDirection.Outbound,
            Status = InteractionStatus.Connected,
            AnsweredUtc = new DateTime(2026, 7, 15, 11, 0, 0, DateTimeKind.Utc),
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderCallId = "call-existing",
            Direction = InteractionDirection.Outbound,
            State = ContactCenterCallState.Connected,
            AnsweredUtc = new DateTime(2026, 7, 15, 11, 0, 0, DateTimeKind.Utc),
            LastProviderEventUtc = new DateTime(2026, 7, 15, 11, 0, 0, DateTimeKind.Utc),
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(m => m.FindByProviderInteractionIdAsync("ProviderA", "call-new", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        interactionManager
            .Setup(m => m.UpdateAsync(interaction, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(m => m.FindByProviderCallIdAsync("ProviderA", "call-new", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CallSession)null);
        callSessionManager
            .Setup(m => m.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        callSessionManager
            .Setup(m => m.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(s => s.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc));

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

        // Act — event for "call-new" finds session bound to "call-existing"
        var ingestResult = await service.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = "ProviderA",
            ProviderCallId = "call-new",
            State = ContactCenterCallState.Ended,
            IdempotencyKey = "ended-new",
        }, TestContext.Current.CancellationToken);

        // Assert — event must be refused (mis-binding prevented)
        Assert.Null(ingestResult);

        // Assert — existing ProviderCallId must not be overwritten
        Assert.Equal("call-existing", session.ProviderCallId);

        // Assert — session state must not be mutated by the mis-bound event
        Assert.Equal(ContactCenterCallState.Connected, session.State);

        // Assert — no mutation was persisted to the wrong session
        callSessionManager.Verify(
            m => m.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
        IContactCenterScopeExecutor? scopeExecutor = null,
        ISession? session = null,
        IDistributedLock? distributedLock = null)
    {
        providerCommandStateService ??= new Mock<IProviderCommandStateService>(MockBehavior.Strict).Object;
        scopeExecutor ??= new Mock<IContactCenterScopeExecutor>(MockBehavior.Strict).Object;
        session ??= new Mock<ISession>().Object;
        distributedLock ??= CreateDistributedLock();

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
            session,
            distributedLock,
            clock,
            logger);
    }

    private static IDistributedLock CreateDistributedLock()
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(service => service.TryAcquireLockAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, true));

        return distributedLock.Object;
    }

    private sealed class TestLocker(Action onDispose) : ILocker
    {
        private readonly Action _onDispose = onDispose;

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            DisposeCore();
        }

        public ValueTask DisposeAsync()
        {
            DisposeCore();

            return ValueTask.CompletedTask;
        }

        private void DisposeCore()
        {
            if (IsDisposed)
            {
                return;
            }

            _onDispose();
            IsDisposed = true;
        }
    }

    private sealed class TestDistributedLock : IDistributedLock
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

        public async Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
        {
            var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            return new TestLocker(() => semaphore.Release());
        }

        public async Task<(ILocker locker, bool locked)> TryAcquireLockAsync(
            string key,
            TimeSpan timeout,
            TimeSpan? expiration = null)
        {
            var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            var locked = await semaphore.WaitAsync(timeout);

            return locked
                ? (new TestLocker(() => semaphore.Release()), true)
                : (null, false);
        }

        public Task<bool> IsLockAcquiredAsync(string key)
        {
            return Task.FromResult(
                _locks.TryGetValue(key, out var semaphore) &&
                semaphore.CurrentCount == 0);
        }
    }
}
