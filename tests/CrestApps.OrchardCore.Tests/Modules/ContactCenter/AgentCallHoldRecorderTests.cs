#nullable enable annotations

using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A provider that holds calls in the agent's browser never reports the hold, so the soft phone's own hold and resume
/// are the only record of it. These pin that they reach the call's hold time and the audit log exactly once each, that
/// the provider still calling the call connected does not end the hold, and that the call ending closes it.
/// </summary>
public sealed class AgentCallHoldRecorderTests
{
    private static readonly DateTime _answeredUtc = new(2026, 9, 23, 21, 9, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Hold_PutsTheCallOnHold_AndRecordsCallHeldAtTheMomentTheAgentAsked()
    {
        // Arrange
        var harness = new Harness();
        var heldUtc = _answeredUtc.AddSeconds(63).AddTicks(1234567);

        // Act
        var recorded = await harness.HoldAsync(heldUtc);

        // Assert
        Assert.True(recorded);
        Assert.True(harness.Session.IsOnHold);
        Assert.True(harness.Session.HoldPlacedByAgent);
        Assert.Equal(heldUtc, harness.Session.HoldStartedUtc);
        Assert.Equal(VoiceCallState.OnHold, harness.Session.State);
        Assert.Equal(InteractionStatus.Held, harness.Interaction.Status);

        var held = Assert.Single(harness.Published);
        Assert.Equal(ContactCenterConstants.Events.CallHeld, held.EventType);
        Assert.Equal(heldUtc, held.OccurredUtc);
        Assert.Equal(ContactCenterActorType.Agent, held.ActorType);
        Assert.Equal("user-1", held.ActorId);
        Assert.Equal("interaction-1", held.InteractionId);

        var data = held.GetData<CallLifecycleEventData>();
        Assert.Equal("agent-1", data.AgentId);
        Assert.Equal(nameof(VoiceCallState.Connected), data.PreviousState);
        Assert.Null(data.DurationSeconds);
    }

    [Fact]
    public async Task Resume_RecordsCallResumedWithHowLongTheHoldLasted_AndAddsItToTheCallsHoldTime()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await harness.HoldAsync(_answeredUtc.AddSeconds(10));
        var recorded = await harness.ResumeAsync(_answeredUtc.AddSeconds(42.5));

        // Assert
        Assert.True(recorded);
        Assert.False(harness.Session.IsOnHold);
        Assert.False(harness.Session.HoldPlacedByAgent);
        Assert.Equal(32.5, harness.Session.HoldSeconds, precision: 3);
        Assert.Equal(VoiceCallState.Connected, harness.Session.State);
        Assert.Equal(InteractionStatus.Connected, harness.Interaction.Status);

        var resumed = Assert.Single(harness.Published, e => e.EventType == ContactCenterConstants.Events.CallResumed);
        Assert.Equal(_answeredUtc.AddSeconds(42.5), resumed.OccurredUtc);
        Assert.Equal(32.5, resumed.GetData<CallLifecycleEventData>().DurationSeconds!.Value, precision: 3);
    }

    [Fact]
    public async Task SeveralHolds_AccumulateOnTheCall()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await harness.HoldAsync(_answeredUtc.AddSeconds(10));
        await harness.ResumeAsync(_answeredUtc.AddSeconds(20));
        await harness.HoldAsync(_answeredUtc.AddSeconds(30));
        await harness.ResumeAsync(_answeredUtc.AddSeconds(45));

        // Assert
        Assert.Equal(25, harness.Session.HoldSeconds, precision: 3);
        Assert.Equal(2, harness.Published.Count(e => e.EventType == ContactCenterConstants.Events.CallHeld));
        Assert.Equal(2, harness.Published.Count(e => e.EventType == ContactCenterConstants.Events.CallResumed));
    }

    [Fact]
    public async Task RetriedHold_IsRecordedOnce_AndKeepsTheFirstHoldsStart()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await harness.HoldAsync(_answeredUtc.AddSeconds(10));
        var retried = await harness.HoldAsync(_answeredUtc.AddSeconds(11));

        // Assert
        Assert.False(retried);
        Assert.Equal(_answeredUtc.AddSeconds(10), harness.Session.HoldStartedUtc);
        Assert.Single(harness.Published);
    }

    [Fact]
    public async Task RetriedResume_IsRecordedOnce_AndCountsTheHoldOnce()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await harness.HoldAsync(_answeredUtc.AddSeconds(10));
        await harness.ResumeAsync(_answeredUtc.AddSeconds(20));
        var retried = await harness.ResumeAsync(_answeredUtc.AddSeconds(21));

        // Assert
        Assert.False(retried);
        Assert.Equal(10, harness.Session.HoldSeconds, precision: 3);
        Assert.Single(harness.Published, e => e.EventType == ContactCenterConstants.Events.CallResumed);
    }

    [Fact]
    public async Task TheHoldAndItsResume_AreKeyedOnTheHold_SoTheLogDeduplicatesARepeat()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await harness.HoldAsync(_answeredUtc.AddSeconds(10));
        await harness.ResumeAsync(_answeredUtc.AddSeconds(20));

        // Assert
        var stamp = _answeredUtc.AddSeconds(10).Ticks;
        Assert.Contains(harness.Published, e => e.IdempotencyKey == $"agent-hold:{ContactCenterConstants.Events.CallHeld}:session-1:{stamp}");
        Assert.Contains(harness.Published, e => e.IdempotencyKey == $"agent-hold:{ContactCenterConstants.Events.CallResumed}:session-1:{stamp}");
    }

    [Fact]
    public async Task TheProviderReportingTheCallConnected_DoesNotEndTheAgentsHold()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await harness.HoldAsync(_answeredUtc.AddSeconds(10));
        await harness.IngestAsync(VoiceCallState.Connected, _answeredUtc.AddSeconds(13), "bridged");

        // Assert
        Assert.True(harness.Session.IsOnHold);
        Assert.Equal(VoiceCallState.OnHold, harness.Session.State);
        Assert.DoesNotContain(harness.Published, e => e.EventType == ContactCenterConstants.Events.CallResumed);
    }

    [Fact]
    public async Task CallEnded_TalkTimeExcludesTheAgentsHolds()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await harness.HoldAsync(_answeredUtc.AddSeconds(10));
        await harness.ResumeAsync(_answeredUtc.AddSeconds(40));
        await harness.IngestAsync(VoiceCallState.Ended, _answeredUtc.AddSeconds(100), "hangup");

        // Assert
        Assert.Equal(30, harness.Session.HoldSeconds, precision: 3);
        Assert.Equal(70, harness.Session.TalkSeconds, precision: 3);
    }

    [Fact]
    public async Task CallEndedWhileTheAgentHoldsIt_ClosesTheHold()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await harness.HoldAsync(_answeredUtc.AddSeconds(10));
        await harness.IngestAsync(VoiceCallState.Ended, _answeredUtc.AddSeconds(50), "hangup");

        // Assert
        Assert.False(harness.Session.IsOnHold);
        Assert.False(harness.Session.HoldPlacedByAgent);
        Assert.Equal(40, harness.Session.HoldSeconds, precision: 3);
        Assert.Equal(10, harness.Session.TalkSeconds, precision: 3);

        var ended = Assert.Single(harness.Published, e => e.EventType == ContactCenterConstants.Events.CallEnded);
        Assert.Equal("40", ended.GetData<CallLifecycleEventData>().Details["holdSeconds"]);
    }

    [Fact]
    public async Task ResumeAfterTheCallEnded_RecordsNothing()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await harness.HoldAsync(_answeredUtc.AddSeconds(10));
        await harness.IngestAsync(VoiceCallState.Ended, _answeredUtc.AddSeconds(50), "hangup");
        var recorded = await harness.ResumeAsync(_answeredUtc.AddSeconds(60));

        // Assert
        Assert.False(recorded);
        Assert.Equal(40, harness.Session.HoldSeconds, precision: 3);
    }

    [Fact]
    public async Task AnotherAgentsCall_IsNotRecorded()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var recorded = await harness.Recorder.RecordAsync(Harness.Change(true, _answeredUtc.AddSeconds(10), userId: "user-2"), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(recorded);
        Assert.False(harness.Session.IsOnHold);
        Assert.Empty(harness.Published);
    }

    [Fact]
    public async Task ACallTheContactCenterDoesNotTrack_IsNotRecorded()
    {
        // Arrange
        var harness = new Harness();
        var change = Harness.Change(true, _answeredUtc.AddSeconds(10));
        change.InteractionId = "extension-call";
        change.CallId = "extension-call-1";

        // Act
        var recorded = await harness.Recorder.RecordAsync(change, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(recorded);
        Assert.Empty(harness.Published);
    }

    [Fact]
    public async Task ACallThatWasNeverAnswered_CannotBeHeld()
    {
        // Arrange
        var harness = new Harness(ringing: true);

        // Act
        var recorded = await harness.HoldAsync(_answeredUtc.AddSeconds(10));

        // Assert
        Assert.False(recorded);
        Assert.False(harness.Session.IsOnHold);
        Assert.Empty(harness.Published);
    }

    private sealed class Harness
    {
        public Harness(bool ringing = false)
        {
            Interaction = new Interaction
            {
                ItemId = "interaction-1",
                ProviderName = "ProviderA",
                ProviderInteractionId = "call-1",
                Channel = InteractionChannel.Voice,
                Direction = InteractionDirection.Inbound,
                AgentId = "agent-1",
                QueueId = "queue-1",
                AnsweredUtc = ringing ? null : _answeredUtc,
            }.RestorePersistedStatus(ringing ? InteractionStatus.Ringing : InteractionStatus.Connected);

            Session = new CallSession
            {
                ItemId = "session-1",
                InteractionId = "interaction-1",
                ProviderName = "ProviderA",
                ProviderCallId = "call-1",
                AgentId = "agent-1",
                Direction = InteractionDirection.Inbound,
                StartedUtc = _answeredUtc,
                AnsweredUtc = ringing ? null : _answeredUtc,
                LastProviderEventUtc = _answeredUtc,
            }.RestorePersistedState(ringing ? VoiceCallState.Ringing : VoiceCallState.Connected);

            var interactionManager = new Mock<IInteractionManager>();
            interactionManager
                .Setup(manager => manager.FindByProviderInteractionIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Interaction);
            interactionManager
                .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Interaction);

            var callSessionManager = new Mock<ICallSessionManager>();
            callSessionManager
                .Setup(manager => manager.FindByProviderCallIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Session);
            callSessionManager
                .Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Session);

            var agentManager = new Mock<IAgentProfileManager>();
            agentManager
                .Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });
            agentManager
                .Setup(manager => manager.FindByUserIdAsync("user-2", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentProfile { ItemId = "agent-2", UserId = "user-2" });

            var eventStore = new Mock<IInteractionEventStore>();
            eventStore
                .Setup(store => store.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var publisher = new Mock<IContactCenterEventPublisher>();
            publisher
                .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
                .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => Published.Add(interactionEvent))
                .Returns(Task.CompletedTask);

            var distributedLock = new Mock<IDistributedLock>();
            distributedLock
                .Setup(service => service.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync((null, true));

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_answeredUtc.AddHours(1));

            var gate = new VoiceIngressGate(distributedLock.Object);
            var session = new Mock<ISession>();

            Recorder = new AgentCallHoldRecorder(
                callSessionManager.Object,
                interactionManager.Object,
                agentManager.Object,
                new ContactCenterAuditRecorder(publisher.Object, clock.Object),
                gate,
                session.Object,
                NullLogger<AgentCallHoldRecorder>.Instance);

            ProviderEvents = new ProviderVoiceEventService(
                interactionManager.Object,
                callSessionManager.Object,
                agentManager.Object,
                new Mock<IContactCenterVoiceProviderResolver>().Object,
                new Mock<ITelephonyProviderResolver>().Object,
                eventStore.Object,
                publisher.Object,
                new Mock<IAgentPresenceManager>().Object,
                new ProviderIdentityResolver([]),
                new Mock<IProviderCommandStateService>().Object,
                new Mock<IContactCenterScopeExecutor>().Object,
                session.Object,
                gate,
                clock.Object,
                NullLogger<ProviderVoiceEventService>.Instance);
        }

        public Interaction Interaction { get; }

        public CallSession Session { get; }

        public List<InteractionEvent> Published { get; } = [];

        public AgentCallHoldRecorder Recorder { get; }

        public ProviderVoiceEventService ProviderEvents { get; }

        public static TelephonyCallHoldChange Change(bool isOnHold, DateTime changedUtc, string userId = "user-1")
            => new()
            {
                CallId = "call-1",
                ProviderName = "ProviderA",
                InteractionId = "interaction-1",
                UserId = userId,
                IsOnHold = isOnHold,
                ChangedUtc = changedUtc,
            };

        public Task<bool> HoldAsync(DateTime changedUtc)
            => Recorder.RecordAsync(Change(true, changedUtc), TestContext.Current.CancellationToken);

        public Task<bool> ResumeAsync(DateTime changedUtc)
            => Recorder.RecordAsync(Change(false, changedUtc), TestContext.Current.CancellationToken);

        public Task<CallSession> IngestAsync(VoiceCallState state, DateTime occurredUtc, string key)
            => ProviderEvents.IngestAsync(new ProviderVoiceEvent
            {
                ProviderName = "ProviderA",
                ProviderCallId = "call-1",
                State = state,
                OccurredUtc = occurredUtc,
                IdempotencyKey = key,
                Metadata = new Dictionary<string, string>(),
            }, TestContext.Current.CancellationToken);
    }
}
