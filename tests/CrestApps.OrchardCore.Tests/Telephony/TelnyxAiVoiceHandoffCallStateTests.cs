using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Modules.ContactCenter;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// A caller an automated AI voice agent handed to the Contact Center, joined to an agent and then hanging up. The
/// caller's leg keeps the AI's client state for the whole call, and until the fix its every event was hidden from
/// the Contact Center: the call stayed ringing through the conversation, ended as an unanswered call by
/// reconciliation, recorded no talk time, and the agent skipped wrap-up.
/// </summary>
public sealed class TelnyxAiVoiceHandoffCallStateTests
{
    private const string CallerCallId = "caller-1";
    private const string AgentLegId = "agent-leg-1";

    private static readonly DateTime _acceptedUtc = new(2026, 9, 24, 4, 9, 48, DateTimeKind.Utc);
    private static readonly DateTime _bridgedUtc = _acceptedUtc.AddSeconds(2);
    private static readonly DateTime _hungUpUtc = _bridgedUtc.AddSeconds(45);

    [Theory]
    // The agent's device answered the pre-dialed leg as they clicked, before the accept: the accept joined it and
    // recorded it answered.
    [InlineData(true)]
    // The accept came first; the leg's own answered webhook joined it, and the caller's bridged event can reach the
    // Contact Center before the leg is recorded answered.
    [InlineData(false)]
    public async Task ProcessAsync_WhenAHandedOffCallerIsJoinedToTheAgent_ConnectsTheCallAndItsHangupEndsItWithWrapUp(bool legAnsweredBeforeAccept)
    {
        // Arrange
        var harness = new Harness(legAnsweredBeforeAccept);

        // Act
        var bridged = await harness.Webhooks.ProcessAsync(Harness.CallerEvent("call.bridged", "evt-bridged", _bridgedUtc), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelnyxWebhookResult.Updated, bridged);
        Assert.Equal(VoiceCallState.Connected, harness.Session.State);
        Assert.Equal(_bridgedUtc, harness.Session.AnsweredUtc);
        Assert.Equal(InteractionStatus.Connected, harness.Interaction.Status);
        Assert.Equal(_bridgedUtc, harness.Interaction.AnsweredUtc);
        Assert.Contains(ContactCenterConstants.Events.CallConnected, harness.PublishedEventTypes);

        // Act
        var hangup = Harness.CallerEvent("call.hangup", "evt-hangup", _hungUpUtc);
        hangup.HangupCause = "normal_clearing";
        hangup.HangupSource = "caller";

        var ended = await harness.Webhooks.ProcessAsync(hangup, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelnyxWebhookResult.Updated, ended);
        Assert.Equal(VoiceCallState.Ended, harness.Session.State);
        Assert.Equal(HangupCause.NormalClearing, harness.Session.HangupCause);
        Assert.Equal(45, harness.Session.TalkSeconds);
        Assert.Equal(InteractionStatus.Ended, harness.Interaction.Status);
        Assert.Equal(_hungUpUtc, harness.Interaction.WrapUpStartedUtc);
        Assert.Contains(ContactCenterConstants.Events.CallEnded, harness.PublishedEventTypes);
        harness.Presence.Verify(
            manager => manager.StartWrapUpAsync("agent-1", It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // The conversation still hears every event on its leg.
        Assert.Equal(["call.bridged", "call.hangup"], harness.AiHandler.EventTypes);
    }

    [Fact]
    public async Task ProcessAsync_BeforeTheCallerIsHandedOff_KeepsTheLegOutOfTheContactCenter()
    {
        // Arrange
        var harness = new Harness(legAnsweredBeforeAccept: false);
        harness.Probe.Active = false;

        // Act
        var result = await harness.Webhooks.ProcessAsync(Harness.CallerEvent("call.answered", "evt-answered", _acceptedUtc), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelnyxWebhookResult.Updated, result);
        Assert.Equal(VoiceCallState.Ringing, harness.Session.State);
        Assert.Empty(harness.PublishedEventTypes);
        Assert.Equal(["call.answered"], harness.AiHandler.EventTypes);
    }

    [Fact]
    public async Task ProcessAsync_WhenTheEventIsTheConversationsOwn_KeepsItFromTheContactCenterWithoutAskingAfterTheCall()
    {
        // Arrange
        var harness = new Harness(legAnsweredBeforeAccept: false);

        // Act
        var result = await harness.Webhooks.ProcessAsync(Harness.CallerEvent("call.speak.ended", "evt-speak", _acceptedUtc), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelnyxWebhookResult.Updated, result);
        Assert.Equal(0, harness.Probe.Calls);
        Assert.Equal(VoiceCallState.Ringing, harness.Session.State);
        Assert.Equal(["call.speak.ended"], harness.AiHandler.EventTypes);
    }

    private sealed class Harness
    {
        private readonly List<string> _publishedEventTypes = [];

        public Harness(bool legAnsweredBeforeAccept)
        {
            // The interaction the handoff created and the session the accept opened: ringing, attributed to the agent,
            // with nothing answered yet.
            Interaction = new Interaction
            {
                ItemId = "interaction-1",
                ActivityItemId = "activity-1",
                ProviderName = TelnyxConstants.ProviderTechnicalName,
                ProviderInteractionId = CallerCallId,
                AgentId = "agent-1",
                QueueId = "queue-1",
                Direction = InteractionDirection.Inbound,
                StartedUtc = _acceptedUtc,
            }.RestorePersistedStatus(InteractionStatus.Ringing);

            Session = new CallSession
            {
                ItemId = "session-1",
                InteractionId = "interaction-1",
                ActivityItemId = "activity-1",
                ProviderName = TelnyxConstants.ProviderTechnicalName,
                ProviderCallId = CallerCallId,
                AgentId = "agent-1",
                QueueId = "queue-1",
                Direction = InteractionDirection.Inbound,
                CreatedUtc = _acceptedUtc,
                StartedUtc = _acceptedUtc,
            }.RestorePersistedState(VoiceCallState.Ringing);

            if (legAnsweredBeforeAccept)
            {
                CallTopologyProjector.UpsertLeg(Session, AgentLegId, CallPartyRole.Agent, CallLegStatus.Answered, _acceptedUtc, agentId: "agent-1");
                CallTopologyProjector.EnsureBridge(Session, null, _acceptedUtc);
                CallTopologyProjector.Join(Session, AgentLegId, CallPartyRole.Agent, _acceptedUtc, "agent-1");
            }
            else
            {
                CallTopologyProjector.UpsertLeg(Session, AgentLegId, CallPartyRole.Agent, CallLegStatus.Dialing, _acceptedUtc, agentId: "agent-1");
            }

            var interactionManager = new Mock<IInteractionManager>();
            interactionManager
                .Setup(manager => manager.FindByProviderInteractionIdAsync(TelnyxConstants.ProviderTechnicalName, CallerCallId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Interaction);

            var callSessionManager = new Mock<ICallSessionManager>();
            callSessionManager
                .Setup(manager => manager.FindByProviderCallIdAsync(TelnyxConstants.ProviderTechnicalName, CallerCallId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Session);

            var eventStore = new Mock<IInteractionEventStore>();
            eventStore
                .Setup(store => store.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var publisher = new Mock<IContactCenterEventPublisher>();
            publisher
                .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
                .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => _publishedEventTypes.Add(interactionEvent.EventType))
                .Returns(Task.CompletedTask);

            var distributedLock = new Mock<IDistributedLock>();
            distributedLock
                .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync((null, true));

            var agentManager = new Mock<IAgentProfileManager>();
            var identityResolver = new ProviderIdentityResolver([new TestProviderIdentityProvider(new ProviderIdentity(TelnyxConstants.ProviderTechnicalName))]);
            var ingressGate = new VoiceIngressGate(distributedLock.Object);
            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_hungUpUtc);

            var voiceEvents = new ProviderVoiceEventService(
                interactionManager.Object,
                callSessionManager.Object,
                agentManager.Object,
                new Mock<IContactCenterVoiceProviderResolver>().Object,
                new Mock<ITelephonyProviderResolver>().Object,
                eventStore.Object,
                publisher.Object,
                Presence.Object,
                identityResolver,
                new Mock<IProviderCommandStateService>(MockBehavior.Strict).Object,
                new Mock<IContactCenterScopeExecutor>(MockBehavior.Strict).Object,
                new Mock<ISession>().Object,
                ingressGate,
                clock.Object,
                NullLogger<ProviderVoiceEventService>.Instance);

            var ingestor = new NormalizedVoiceEventIngestor(
                [new ContactCenterVoiceProjection(new ProviderVoiceEventSink(voiceEvents), NullLogger<ContactCenterVoiceProjection>.Instance)],
                identityResolver,
                ingressGate,
                NullLogger<NormalizedVoiceEventIngestor>.Instance);

            var monitor = new Mock<IOptionsMonitor<TelnyxOptions>>();
            monitor.SetupGet(value => value.CurrentValue).Returns(new TelnyxOptions());

            var orchestrator = new TelnyxOutboundBridgeOrchestrator(
                new TelnyxApiClient(
                    new HttpClient(new RefusingHttpMessageHandler()) { BaseAddress = new Uri("https://api.telnyx.test/v2/") },
                    new OptionsWrapper<TelnyxOptions>(new TelnyxOptions { ApiBaseUrl = "https://api.telnyx.test/v2/", ApiKey = "KEY" }),
                    new TelnyxApiRetryPolicy(TimeSpan.Zero),
                    NullLogger<TelnyxApiClient>.Instance),
                NullLogger<TelnyxOutboundBridgeOrchestrator>.Instance,
                monitor.Object,
                new Mock<IContactCenterAgentLegFailureService>(MockBehavior.Strict).Object,
                [AiHandler],
                [],
                [Probe]);

            Webhooks = new TelnyxWebhookService(
                ingestor,
                new Mock<ITelnyxInboundCallRouter>(MockBehavior.Strict).Object,
                new Mock<IInboundVoiceDigitsSink>(MockBehavior.Strict).Object,
                orchestrator,
                [],
                [],
                clock.Object,
                NullLogger<TelnyxWebhookService>.Instance);

            Probe.Interaction = Interaction;
        }

        public Interaction Interaction { get; }

        public CallSession Session { get; }

        public Mock<IAgentPresenceManager> Presence { get; } = new();

        public RecordingAiVoiceEventHandler AiHandler { get; } = new();

        public InteractionProbe Probe { get; } = new();

        public TelnyxWebhookService Webhooks { get; }

        public IReadOnlyList<string> PublishedEventTypes => _publishedEventTypes;

        public static TelnyxCallEvent CallerEvent(string eventType, string eventId, DateTime occurredUtc)
            => new()
            {
                EventType = eventType,
                EventId = eventId,
                CallControlId = CallerCallId,
                CallLegId = "caller-leg-1",
                Direction = "outgoing",
                OccurredUtc = occurredUtc,

                // What the AI voice agent put on the leg when it dialled the customer, echoed on every event since.
                ClientState = new TelnyxOutboundBridgeState
                {
                    Intent = TelnyxOutboundBridgeState.AiVoiceLegIntent,
                    ActivityId = "activity-1",
                }.ToClientStateJson(),
            };
    }

    private sealed class InteractionProbe : IInboundVoiceInteractionProbe
    {
        public bool Active { get; set; } = true;

        public Interaction Interaction { get; set; }

        public int Calls { get; private set; }

        public Task<bool> HasActiveInteractionAsync(string providerName, string providerCallId, CancellationToken cancellationToken = default)
        {
            Calls++;

            return Task.FromResult(
                Active &&
                providerName == TelnyxConstants.ProviderTechnicalName &&
                providerCallId == Interaction?.ProviderInteractionId &&
                Interaction.Status is not (InteractionStatus.Ended or InteractionStatus.Failed));
        }
    }

    private sealed class RecordingAiVoiceEventHandler : ITelnyxAiVoiceEventHandler
    {
        public List<string> EventTypes { get; } = [];

        public Task HandleAsync(TelnyxCallEvent callEvent, TelnyxOutboundBridgeState state, CancellationToken cancellationToken = default)
        {
            EventTypes.Add(callEvent.EventType);

            return Task.CompletedTask;
        }
    }

    private sealed class RefusingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException($"These tests expect no provider HTTP, but {request.Method} {request.RequestUri} was attempted.");
    }
}
