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
/// The provider's call stream is where most of a call's states are learned, so these pin what it writes to the
/// audit log: each hold's length and the hold time a call accumulates, talk time that no longer counts the
/// customer's time on hold, the provider's own time on every record, and the whole of how a call ended.
/// </summary>
public sealed class ProviderVoiceEventServiceAuditTests
{
    private static readonly DateTime _answeredUtc = new(2026, 9, 23, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HoldThenResume_AddsTheHoldToTheCall_AndTheResumeSaysHowLongItLasted()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await harness.IngestAsync(VoiceCallState.OnHold, _answeredUtc.AddSeconds(10), "hold");
        await harness.IngestAsync(VoiceCallState.Connected, _answeredUtc.AddSeconds(40.25), "resume");

        // Assert
        Assert.Equal(30.25, harness.Session.HoldSeconds, precision: 3);
        Assert.False(harness.Session.IsOnHold);

        var resumed = Assert.Single(harness.Published, e => e.EventType == ContactCenterConstants.Events.CallResumed);
        var data = resumed.GetData<CallLifecycleEventData>();
        Assert.Equal(30.25, data.DurationSeconds!.Value, precision: 3);
        Assert.Equal(nameof(VoiceCallState.OnHold), data.PreviousState);
        Assert.Equal("interaction-1", data.InteractionId);
    }

    [Fact]
    public async Task EveryStateEvent_IsDatedByTheProvidersTime_ToTheMillisecond()
    {
        // Arrange
        var harness = new Harness();
        var providerUtc = _answeredUtc.AddSeconds(10).AddMilliseconds(123);

        // Act
        await harness.IngestAsync(VoiceCallState.OnHold, providerUtc, "hold");

        // Assert
        var held = Assert.Single(harness.Published, e => e.EventType == ContactCenterConstants.Events.CallHeld);
        Assert.Equal(providerUtc, held.OccurredUtc);
        Assert.Equal(providerUtc, held.GetData<CallLifecycleEventData>().ProviderOccurredUtc);
        Assert.Equal(ContactCenterActorType.Provider, held.ActorType);
        Assert.All(harness.Published, e => Assert.Equal(providerUtc, e.OccurredUtc));
    }

    [Fact]
    public async Task CallEnded_TalkTimeExcludesHold_AndCarriesHowTheCallEnded()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await harness.IngestAsync(VoiceCallState.OnHold, _answeredUtc.AddSeconds(10), "hold");
        await harness.IngestAsync(VoiceCallState.Connected, _answeredUtc.AddSeconds(40), "resume");
        await harness.IngestAsync(
            VoiceCallState.Ended,
            _answeredUtc.AddSeconds(100),
            "hangup",
            HangupCause.NormalClearing,
            new Dictionary<string, string>
            {
                [ContactCenterConstants.TelephonyMetadata.ProviderHangupCause] = "normal_clearing",
                [ContactCenterConstants.TelephonyMetadata.SipHangupCause] = "200",
                [ContactCenterConstants.TelephonyMetadata.HangupSource] = "callee",
            });

        // Assert
        Assert.Equal(30, harness.Session.HoldSeconds, precision: 3);
        Assert.Equal(70, harness.Session.TalkSeconds, precision: 3);

        var ended = Assert.Single(harness.Published, e => e.EventType == ContactCenterConstants.Events.CallEnded);
        var data = ended.GetData<CallLifecycleEventData>();
        Assert.Equal(nameof(HangupCause.NormalClearing), data.HangupCause);
        Assert.Equal("normal_clearing", data.ProviderHangupCause);
        Assert.Equal("200", data.SipHangupCause);
        Assert.Equal("callee", data.HangupSource);
        Assert.Equal("70", data.Details["talkSeconds"]);
        Assert.Equal("30", data.Details["holdSeconds"]);
        Assert.Equal(_answeredUtc.AddSeconds(100), ended.OccurredUtc);
    }

    [Fact]
    public async Task CallEndedWhileOnHold_CountsTheHoldThatWasRunning()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await harness.IngestAsync(VoiceCallState.OnHold, _answeredUtc.AddSeconds(10), "hold");
        await harness.IngestAsync(VoiceCallState.Ended, _answeredUtc.AddSeconds(50), "hangup");

        // Assert
        Assert.Equal(40, harness.Session.HoldSeconds, precision: 3);
        Assert.Equal(10, harness.Session.TalkSeconds, precision: 3);
    }

    [Fact]
    public async Task RepeatedHoldState_DoesNotRestartTheHold()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await harness.IngestAsync(VoiceCallState.OnHold, _answeredUtc.AddSeconds(10), "hold-1");
        await harness.IngestAsync(VoiceCallState.OnHold, _answeredUtc.AddSeconds(20), "hold-2");
        await harness.IngestAsync(VoiceCallState.Connected, _answeredUtc.AddSeconds(30), "resume");

        // Assert
        Assert.Equal(20, harness.Session.HoldSeconds, precision: 3);
    }

    [Fact]
    public async Task OutboundCallEndingUnanswered_IsRecordedAsADialThatFailed()
    {
        // Arrange
        var harness = new Harness(outboundRinging: true);

        // Act
        await harness.IngestAsync(VoiceCallState.NoAnswer, _answeredUtc.AddSeconds(25), "no-answer", HangupCause.NoAnswer);

        // Assert
        var failed = Assert.Single(harness.Published, e => e.EventType == ContactCenterConstants.Events.DialFailed);
        Assert.Equal("interaction-1", failed.InteractionId);
        Assert.Equal(_answeredUtc.AddSeconds(25), failed.OccurredUtc);
        Assert.Equal(nameof(HangupCause.NoAnswer), failed.GetData<CallLifecycleEventData>().Reason);
    }

    [Fact]
    public async Task RedeliveredProviderEvent_RecordsNothingAgain()
    {
        // Arrange
        var harness = new Harness();
        harness.EventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await harness.IngestAsync(VoiceCallState.OnHold, _answeredUtc.AddSeconds(10), "hold");

        // Assert
        Assert.Empty(harness.Published);
        Assert.False(harness.Session.IsOnHold);
    }

    [Fact]
    public async Task EveryEventTheCallStreamWrites_NamesTheProvider_NotTheAgent()
    {
        // Arrange
        // The agent on the call is what these events are about, and the payload names them. Putting the agent's
        // profile id where the actor goes made a hangup read as the agent's act.
        var harness = new Harness(newSession: true);

        // Act
        await harness.IngestAsync(VoiceCallState.Connected, _answeredUtc.AddSeconds(1), "connected");
        await harness.IngestAsync(VoiceCallState.Ended, _answeredUtc.AddSeconds(90), "hangup", HangupCause.NormalClearing);

        // Assert
        var created = Assert.Single(harness.Published, e => e.EventType == ContactCenterConstants.Events.CallSessionCreated);
        Assert.Equal("agent-1", created.GetData<CallLifecycleEventData>()?.AgentId);

        foreach (var interactionEvent in harness.Published)
        {
            Assert.Equal(ContactCenterActorType.Provider, interactionEvent.ActorType);
            Assert.Equal("ProviderA", interactionEvent.ActorId);
        }

        var ended = Assert.Single(harness.Published, e => e.EventType == ContactCenterConstants.Events.CallEnded);
        Assert.Equal("agent-1", ended.GetData<CallLifecycleEventData>().AgentId);
    }

    [Fact]
    public async Task AHandledCallEnding_RecordsTheEndBeforeTheWrapUpItCauses_AtTheSameInstant()
    {
        // Arrange
        // Live, the wrap-up was written before the hangup that caused it, both at the provider's instant, so an agent
        // timeline that breaks ties by when each was written showed the agent in wrap-up before the call had ended.
        var harness = new Harness(agentOnCall: true);
        var providerEndedUtc = _answeredUtc.AddSeconds(211).AddMilliseconds(839);

        // Act
        await harness.IngestAsync(VoiceCallState.Ended, providerEndedUtc, "hangup", HangupCause.NormalClearing);

        // Assert
        Assert.NotNull(harness.WrapUpContext);
        Assert.Equal(providerEndedUtc, harness.WrapUpContext!.ChangedUtc);
        Assert.Contains(ContactCenterConstants.Events.CallEnded, harness.PublishedBeforeWrapUp);

        var ended = Assert.Single(harness.Published, e => e.EventType == ContactCenterConstants.Events.CallEnded);
        Assert.Equal(providerEndedUtc, ended.OccurredUtc);
        Assert.Equal(providerEndedUtc, ended.GetData<CallLifecycleEventData>().ProviderOccurredUtc);
    }

    private sealed class Harness
    {
        public Harness(bool outboundRinging = false, bool newSession = false, bool agentOnCall = false)
        {
            Interaction = new Interaction
            {
                ItemId = "interaction-1",
                ProviderName = "ProviderA",
                ProviderInteractionId = "call-1",
                Channel = InteractionChannel.Voice,
                Direction = outboundRinging ? InteractionDirection.Outbound : InteractionDirection.Inbound,
                AgentId = "agent-1",
                QueueId = "queue-1",
                AnsweredUtc = outboundRinging ? null : _answeredUtc,
            }.RestorePersistedStatus(outboundRinging ? InteractionStatus.Ringing : InteractionStatus.Connected);

            Session = new CallSession
            {
                ItemId = "session-1",
                InteractionId = "interaction-1",
                ProviderName = "ProviderA",
                ProviderCallId = "call-1",
                Direction = Interaction.Direction,
                StartedUtc = _answeredUtc,
                AnsweredUtc = outboundRinging ? null : _answeredUtc,
                LastProviderEventUtc = _answeredUtc,
                AgentId = agentOnCall ? "agent-1" : null,
            }.RestorePersistedState(outboundRinging ? VoiceCallState.Ringing : VoiceCallState.Connected);

            Presence
                .Setup(manager => manager.StartWrapUpAsync(It.IsAny<string>(), It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()))
                .Callback<string, AgentStateChangeContext, CancellationToken>((_, context, _) =>
                {
                    WrapUpContext = context;
                    PublishedBeforeWrapUp = [.. Published.Select(e => e.EventType)];
                })
                .ReturnsAsync((AgentProfile)null!);

            var interactionManager = new Mock<IInteractionManager>();
            interactionManager
                .Setup(manager => manager.FindByProviderInteractionIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Interaction);

            // A call the platform has not seen a provider event for yet has no session, so the first event makes one.
            var sessionExists = !newSession;
            var callSessionManager = new Mock<ICallSessionManager>();
            callSessionManager
                .Setup(manager => manager.FindByProviderCallIdAsync("ProviderA", "call-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => sessionExists ? Session : null!);
            callSessionManager
                .Setup(manager => manager.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Session);
            callSessionManager
                .Setup(manager => manager.CreateAsync(It.IsAny<CallSession>(), It.IsAny<CancellationToken>()))
                .Callback(() => sessionExists = true)
                .Returns(ValueTask.CompletedTask);

            EventStore
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

            Service = new ProviderVoiceEventService(
                interactionManager.Object,
                callSessionManager.Object,
                new Mock<IAgentProfileManager>().Object,
                new Mock<IContactCenterVoiceProviderResolver>().Object,
                new Mock<ITelephonyProviderResolver>().Object,
                EventStore.Object,
                publisher.Object,
                Presence.Object,
                new ProviderIdentityResolver([]),
                new Mock<IProviderCommandStateService>().Object,
                new Mock<IContactCenterScopeExecutor>().Object,
                new Mock<ISession>().Object,
                new VoiceIngressGate(distributedLock.Object),
                clock.Object,
                NullLogger<ProviderVoiceEventService>.Instance);
        }

        public Interaction Interaction { get; }

        public CallSession Session { get; }

        public Mock<IInteractionEventStore> EventStore { get; } = new();

        public List<InteractionEvent> Published { get; } = [];

        public Mock<IAgentPresenceManager> Presence { get; } = new();

        public AgentStateChangeContext? WrapUpContext { get; private set; }

        public List<string> PublishedBeforeWrapUp { get; private set; } = [];

        public ProviderVoiceEventService Service { get; }

        public Task<CallSession> IngestAsync(
            VoiceCallState state,
            DateTime occurredUtc,
            string key,
            HangupCause? hangupCause = null,
            Dictionary<string, string>? metadata = null)
            => Service.IngestAsync(new ProviderVoiceEvent
            {
                ProviderName = "ProviderA",
                ProviderCallId = "call-1",
                State = state,
                OccurredUtc = occurredUtc,
                IdempotencyKey = key,
                HangupCause = hangupCause,
                Metadata = metadata ?? new Dictionary<string, string>(),
            }, TestContext.Current.CancellationToken);
    }
}
