#nullable enable annotations

using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Guards the bridged Contact Center call that outlived one of its legs. Live, the agent hung up the caller's leg and
/// the agent's own leg (pre-dialed, joined to the caller) stayed up for another 31 seconds; the call, its talk time,
/// its hold time and the agent's wrap-up all waited for it. Either leg hanging up ends the call at that moment, and the
/// platform hangs up the other leg so it cannot linger.
/// </summary>
public sealed class ContactCenterBridgedLegHangupTests
{
    private const string CallerCallId = "caller-1";
    private const string AgentLegId = "agent-leg-1";

    private static readonly DateTime _answeredUtc = new(2026, 9, 24, 14, 4, 49, DateTimeKind.Utc);
    private static readonly DateTime _heldUtc = new(2026, 9, 24, 14, 5, 1, DateTimeKind.Utc);
    private static readonly DateTime _agentHungUpUtc = new(2026, 9, 24, 14, 6, 18, DateTimeKind.Utc);
    private static readonly DateTime _processedUtc = _agentHungUpUtc.AddSeconds(3);

    [Fact]
    public async Task RecordEndedAsync_WhenTheJoinedAgentLegHangsUpFirst_EndsTheCallAtThatMomentAndReleasesTheCaller()
    {
        // Arrange
        var harness = new FailureServiceHarness();

        // Act
        var ended = await harness.Service.RecordEndedAsync(
            TelnyxConstants.ProviderTechnicalName,
            CallerCallId,
            AgentLegId,
            _agentHungUpUtc,
            HangupCause.NormalClearing,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(ended);

        // The call ends through the same path as any provider ending, dated when the agent's leg hung up rather than
        // when the webhook happened to be processed, so talk and hold stop at the real end.
        var ending = Assert.Single(harness.IngestedEvents);
        Assert.Equal(CallerCallId, ending.ProviderCallId);
        Assert.Equal(AgentLegId, ending.ProviderLegId);
        Assert.Equal(VoiceCallState.Ended, ending.State);
        Assert.Equal(HangupCause.NormalClearing, ending.HangupCause);
        Assert.Equal(_agentHungUpUtc, ending.OccurredUtc);
        Assert.False(string.IsNullOrEmpty(ending.IdempotencyKey));

        harness.Telephony.Verify(
            telephony => telephony.HangupAsync(It.Is<CallReference>(call => call.CallId == CallerCallId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    // The leg was rung but never joined to the caller: that is a connect failure, which is FailAsync's business.
    [InlineData("not-joined")]
    // The call has since moved to another agent (a transfer): the first agent's leg leaving does not end it.
    [InlineData("another-agent")]
    // The call already ended (the caller hung up first); this is the teardown of its agent leg.
    [InlineData("already-ended")]
    public async Task RecordEndedAsync_WhenTheLegNoLongerCarriesTheCall_LeavesTheCallAlone(string scenario)
    {
        // Arrange
        var harness = new FailureServiceHarness(scenario);

        // Act
        var ended = await harness.Service.RecordEndedAsync(
            TelnyxConstants.ProviderTechnicalName,
            CallerCallId,
            AgentLegId,
            _agentHungUpUtc,
            HangupCause.NormalClearing,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(ended);
        Assert.Empty(harness.IngestedEvents);
        harness.Telephony.Verify(
            telephony => telephony.HangupAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(TelnyxOutboundBridgeState.ContactCenterAgentLegIntent)]
    [InlineData(TelnyxOutboundBridgeState.ContactCenterPreDialedAgentLegIntent)]
    public async Task AdvanceAsync_WhenAnAgentLegClearsNormally_ReportsTheEndAgainstTheCallerWithTheLegsOwnTime(string intent)
    {
        // Arrange
        var failureService = new Mock<IContactCenterAgentLegFailureService>();
        failureService
            .Setup(service => service.RecordEndedAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<HangupCause?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var coordinator = new Mock<IAgentPreDialCoordinator>();
        var orchestrator = CreateOrchestrator(failureService.Object, coordinator.Object);

        // Act
        await orchestrator.AdvanceAsync(new TelnyxCallEvent
        {
            EventType = "call.hangup",
            CallControlId = AgentLegId,
            HangupCause = "normal_clearing",
            SipHangupCause = "200",
            HangupSource = "caller",
            OccurredUtc = _agentHungUpUtc,
            ClientState = new TelnyxOutboundBridgeState
            {
                Intent = intent,
                PeerCallControlId = CallerCallId,
                ReservationId = "reservation-1",
            }.ToClientStateJson(),
        }, TestContext.Current.CancellationToken);

        // Assert
        failureService.Verify(
            service => service.RecordEndedAsync(
                TelnyxConstants.ProviderTechnicalName,
                CallerCallId,
                AgentLegId,
                _agentHungUpUtc,
                HangupCause.NormalClearing,
                It.IsAny<CancellationToken>()),
            Times.Once);
        failureService.Verify(
            service => service.FailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HangupCause?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenTheCallerHangsUpFirst_HangsUpTheAgentLegStillJoinedToTheCall()
    {
        // Arrange
        var session = CreateSession();

        // The caller's hangup ended the call and, with it, every leg the call still had. The agent's leg was one of
        // them: it did not hang up itself, so nothing says the provider has released it.
        CallTopologyProjector.EndLeg(session, "caller-leg-1", _agentHungUpUtc, HangupCause.NormalClearing);
        CallTopologyProjector.EndRemainingLegs(session, _agentHungUpUtc);
        session.TransitionTo(VoiceCallState.Ended);

        var provider = new RecordingLegReleaseProvider();
        var handler = CreateReleaseHandler(session, provider);

        // Act
        await handler.HandleAsync(CallEnded(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([AgentLegId], provider.Released);
    }

    [Fact]
    public async Task HandleAsync_WhenTheAgentLegEndedTheCallItself_DoesNotHangItUpAgain()
    {
        // Arrange
        var session = CreateSession();
        CallTopologyProjector.EndLeg(session, AgentLegId, _agentHungUpUtc, HangupCause.NormalClearing);
        CallTopologyProjector.EndRemainingLegs(session, _agentHungUpUtc);
        session.TransitionTo(VoiceCallState.Ended);

        var provider = new RecordingLegReleaseProvider();
        var handler = CreateReleaseHandler(session, provider);

        // Act
        await handler.HandleAsync(CallEnded(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(provider.Released);
    }

    [Fact]
    public async Task HandleAsync_ForAnyEventOtherThanTheCallEnding_DoesNothing()
    {
        // Arrange
        var session = CreateSession();
        var provider = new RecordingLegReleaseProvider();
        var handler = CreateReleaseHandler(session, provider);

        // Act
        await handler.HandleAsync(new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.CallHeld,
            InteractionId = "interaction-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(provider.Released);
    }

    private static InteractionEvent CallEnded()
        => new()
        {
            EventType = ContactCenterConstants.Events.CallEnded,
            InteractionId = "interaction-1",
            AggregateId = "interaction-1",
        };

    private static CallSession CreateSession()
    {
        var session = new CallSession
        {
            ItemId = "call-session-1",
            InteractionId = "interaction-1",
            ProviderName = TelnyxConstants.ProviderTechnicalName,
            ProviderCallId = CallerCallId,
            AgentId = "agent-1",
            QueueId = "queue-1",
            Direction = InteractionDirection.Inbound,
            AnsweredUtc = _answeredUtc,
        }.RestorePersistedState(VoiceCallState.OnHold);

        CallTopologyProjector.UpsertLeg(session, "caller-leg-1", CallPartyRole.Customer, CallLegStatus.Answered, _answeredUtc);
        CallTopologyProjector.UpsertLeg(session, AgentLegId, CallPartyRole.Agent, CallLegStatus.Answered, _answeredUtc, agentId: "agent-1");
        CallTopologyProjector.EnsureBridge(session, null, _answeredUtc);
        CallTopologyProjector.Join(session, "caller-leg-1", CallPartyRole.Customer, _answeredUtc);
        CallTopologyProjector.Join(session, AgentLegId, CallPartyRole.Agent, _answeredUtc, agentId: "agent-1");
        CallSessionHoldsProbe.Hold(session, _heldUtc);

        return session;
    }

    private static ContactCenterAgentLegReleaseHandler CreateReleaseHandler(CallSession session, RecordingLegReleaseProvider provider)
    {
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var resolver = new Mock<IContactCenterVoiceProviderResolver>();
        resolver.Setup(value => value.Get(TelnyxConstants.ProviderTechnicalName)).Returns(provider);

        return new ContactCenterAgentLegReleaseHandler(
            callSessionManager.Object,
            resolver.Object,
            NullLogger<ContactCenterAgentLegReleaseHandler>.Instance);
    }

    private static TelnyxOutboundBridgeOrchestrator CreateOrchestrator(
        IContactCenterAgentLegFailureService failureService,
        IAgentPreDialCoordinator coordinator)
    {
        var monitor = new Mock<IOptionsMonitor<TelnyxOptions>>();
        monitor.SetupGet(value => value.CurrentValue).Returns(new TelnyxOptions());

        return new TelnyxOutboundBridgeOrchestrator(
            new TelnyxApiClient(
                new HttpClient(new RefusingHttpMessageHandler()) { BaseAddress = new Uri("https://api.telnyx.test/v2/") },
                new OptionsWrapper<TelnyxOptions>(new TelnyxOptions { ApiBaseUrl = "https://api.telnyx.test/v2/", ApiKey = "KEY" }),
                new TelnyxApiRetryPolicy(TimeSpan.Zero),
                NullLogger<TelnyxApiClient>.Instance),
            NullLogger<TelnyxOutboundBridgeOrchestrator>.Instance,
            monitor.Object,
            failureService,
            [],
            [coordinator],
            []);
    }

    private sealed class FailureServiceHarness
    {
        public FailureServiceHarness(string scenario = null)
        {
            var interaction = new Interaction
            {
                ItemId = "interaction-1",
                ProviderName = TelnyxConstants.ProviderTechnicalName,
                ProviderInteractionId = CallerCallId,
                AgentId = "agent-1",
                QueueId = "queue-1",
                Direction = InteractionDirection.Inbound,
                AnsweredUtc = _answeredUtc,
            }.RestorePersistedStatus(InteractionStatus.Held);

            var session = CreateSession();

            switch (scenario)
            {
                case "not-joined":
                    session.Legs.Remove(session.Legs.Single(leg => leg.ProviderLegId == AgentLegId));
                    session.Bridge.Participants.Clear();
                    CallTopologyProjector.UpsertLeg(session, AgentLegId, CallPartyRole.Agent, CallLegStatus.Dialing, _answeredUtc, agentId: "agent-1");
                    break;
                case "another-agent":
                    session.AgentId = "agent-2";
                    CallTopologyProjector.UpsertLeg(session, "agent-leg-2", CallPartyRole.Agent, CallLegStatus.Answered, _heldUtc, agentId: "agent-2");
                    CallTopologyProjector.Join(session, "agent-leg-2", CallPartyRole.Agent, _heldUtc, agentId: "agent-2");
                    break;
                case "already-ended":
                    interaction.TransitionTo(InteractionStatus.Ended);
                    interaction.EndedUtc = _agentHungUpUtc;
                    break;
            }

            var interactionManager = new Mock<IInteractionManager>();
            interactionManager
                .Setup(manager => manager.FindByProviderInteractionIdAsync(TelnyxConstants.ProviderTechnicalName, CallerCallId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(interaction);

            var callSessionManager = new Mock<ICallSessionManager>();
            callSessionManager
                .Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);

            Telephony
                .Setup(service => service.HangupAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TelephonyResult.Success(new TelephonyCall { CallId = CallerCallId }));

            var voiceEvents = new Mock<IProviderVoiceEventService>();
            voiceEvents
                .Setup(service => service.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()))
                .Callback<ProviderVoiceEvent, CancellationToken>((providerEvent, _) => IngestedEvents.Add(providerEvent))
                .ReturnsAsync(session);

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_processedUtc);

            Service = new ContactCenterAgentLegFailureService(
                interactionManager.Object,
                callSessionManager.Object,
                Telephony.Object,
                new RecordingContactCenterAuditRecorder(),
                voiceEvents.Object,
                clock.Object,
                NullLogger<ContactCenterAgentLegFailureService>.Instance,
            new Mock<IAgentPresenceManager>().Object);
        }

        public ContactCenterAgentLegFailureService Service { get; }

        public Mock<ITelephonyService> Telephony { get; } = new();

        public List<ProviderVoiceEvent> IngestedEvents { get; } = [];
    }

    private sealed class RecordingLegReleaseProvider : IContactCenterVoiceProvider, IContactCenterVoiceAgentLegReleaseProvider
    {
        public List<string> Released { get; } = [];

        public string TechnicalName => TelnyxConstants.ProviderTechnicalName;

        public Microsoft.Extensions.Localization.LocalizedString Name => new("Telnyx", "Telnyx");

        public VoiceProviderDeliveryModel DeliveryModel => VoiceProviderDeliveryModel.ServerSideAcd;

        public ContactCenterVoiceProviderCapabilities Capabilities => ContactCenterVoiceProviderCapabilities.AgentConnect;

        public Task ReleaseAgentLegAsync(string agentLegId, CancellationToken cancellationToken = default)
        {
            Released.Add(agentLegId);

            return Task.CompletedTask;
        }
    }

    private sealed class RefusingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException($"These tests expect no provider HTTP, but {request.Method} {request.RequestUri} was attempted.");
    }

    private static class CallSessionHoldsProbe
    {
        public static void Hold(CallSession session, DateTime heldUtc)
        {
            session.IsOnHold = true;
            session.HoldStartedUtc = heldUtc;
            session.HoldPlacedByAgent = true;
        }
    }
}
