#nullable enable annotations

using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An inbound caller waiting in a queue whose agent accepted the offer but could not be reached: the soft phone's
/// registration had lapsed while the Contact Center hub stayed connected, so every leg rung to it was refused (SIP
/// 480). The call used to be settled as failed and the caller hung up on; the caller now gets what the queue does with
/// an offer nobody answers, and the agent is set Away.
/// </summary>
public sealed class ContactCenterAgentLegFailureQueuedCallerTests
{
    private static readonly DateTime _now = new(2026, 10, 14, 9, 30, 0, DateTimeKind.Utc);

    private readonly RecordingContactCenterAuditRecorder _auditRecorder = new();

    [Fact]
    public async Task FailAsync_WhenAQueuedInboundCallersAcceptedAgentCannotBeReached_ReleasesTheCallerThroughTheQueueInsteadOfFailingTheCall()
    {
        // Arrange
        var interaction = CreateQueuedInboundInteraction("q1");
        var session = CreateSessionWithDialingAgentLeg();
        var reservation = CreateAcceptedReservation("q1");
        var order = new List<string>();

        var interactionManager = CreateInteractionManager(interaction);
        interactionManager
            .Setup(manager => manager.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("interaction-update"))
            .Returns(ValueTask.CompletedTask);

        var callSessionManager = CreateCallSessionManager(session);
        callSessionManager
            .Setup(manager => manager.UpdateAsync(session, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("session-update"))
            .Returns(ValueTask.CompletedTask);

        var reservationManager = CreateReservationManager("act-1", reservation);
        var reservationService = new Mock<IActivityReservationService>(MockBehavior.Strict);
        reservationService
            .Setup(service => service.ReleaseUnreachableAgentAsync("r1", It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("release"))
            .ReturnsAsync(reservation);

        var telephonyService = new Mock<ITelephonyService>(MockBehavior.Strict);
        var presenceManager = new Mock<IAgentPresenceManager>(MockBehavior.Strict);
        var abandonmentTracker = new Mock<IDialerAbandonmentTracker>(MockBehavior.Strict);
        var service = CreateService(
            interactionManager,
            callSessionManager,
            telephonyService,
            reservationManager,
            reservationService,
            presenceManager,
            abandonmentTracker);

        // Act
        var failed = await service.FailAsync("Telnyx", "call-1", HangupCause.NoAnswer, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(failed);
        reservationService.Verify(service => service.ReleaseUnreachableAgentAsync("r1", It.IsAny<CancellationToken>()), Times.Once);

        // The agent is set Away by the release, not returned to work here, and no abandoned-call message is played to
        // a caller who is still in the queue or on their way to voicemail.
        presenceManager.VerifyNoOtherCalls();
        abandonmentTracker.VerifyNoOtherCalls();

        // The call goes on: the caller is in the queue again or on their way to voicemail, so the interaction is not
        // settled as failed and the caller is not hung up on.
        Assert.Equal(InteractionStatus.Ringing, interaction.Status);
        Assert.Null(interaction.EndedUtc);
        Assert.Equal(VoiceCallState.Ringing, session.State);
        Assert.Null(session.EndedUtc);
        telephonyService.Verify(telephony => telephony.HangupAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()), Times.Never);

        // Only the agent's leg ends, and the call no longer names the agent who was never reached.
        var agentLeg = Assert.Single(session.Legs, leg => leg.ProviderLegId == "agent-leg-1");
        Assert.Equal(CallLegStatus.Failed, agentLeg.Status);
        Assert.Equal(_now, agentLeg.EndedUtc);
        Assert.Equal(HangupCause.NoAnswer, agentLeg.HangupCause);
        var customerLeg = Assert.Single(session.Legs, leg => leg.ProviderLegId == "call-1");
        Assert.Null(customerLeg.EndedUtc);
        Assert.Null(session.AgentId);

        // The session is written before the release commits the unit of work; the interaction, which the release
        // itself re-reads and writes, is never written from the copy read here.
        Assert.Equal(["session-update", "release"], order);

        var legFailed = Assert.Single(_auditRecorder.CallsOf(ContactCenterConstants.Events.AgentLegFailed));
        Assert.Equal("interaction-1", legFailed.Data.InteractionId);
        Assert.Equal("agent-leg-1", legFailed.Data.ProviderLegId);
        Assert.Equal("a1", legFailed.Data.AgentId);
        Assert.Equal(nameof(CallLegStatus.Failed), legFailed.Data.State);
        Assert.Equal(nameof(HangupCause.NoAnswer), legFailed.Data.HangupCause);
        Assert.Equal(CallLifecycleReasons.AgentLegFailed, legFailed.Data.Reason);
        Assert.Equal(_now, legFailed.OccurredUtc);
    }

    [Fact]
    public async Task FailAsync_WhenTheCallerWasReleased_DoesNotWriteTheCopiesReadBeforeTheRelease()
    {
        // Arrange
        // The release commits the unit of work and publishes events whose handlers update the interaction in another
        // scope. A copy read before the release and written after it is refused as a concurrent change; the service
        // must not write one. The store here refuses every write made after the release, as the real one would.
        var interaction = CreateQueuedInboundInteraction("q1");
        var session = CreateSessionWithDialingAgentLeg();
        var reservation = CreateAcceptedReservation("q1");
        var released = false;

        var interactionManager = CreateInteractionManager(interaction);
        interactionManager
            .Setup(manager => manager.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                if (released)
                {
                    throw new ConcurrencyException(new Document());
                }
            })
            .Returns(ValueTask.CompletedTask);

        var callSessionManager = CreateCallSessionManager(session);
        callSessionManager
            .Setup(manager => manager.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                if (released)
                {
                    throw new ConcurrencyException(new Document());
                }
            })
            .Returns(ValueTask.CompletedTask);

        var reservationManager = CreateReservationManager("act-1", reservation);
        var reservationService = new Mock<IActivityReservationService>(MockBehavior.Strict);
        reservationService
            .Setup(service => service.ReleaseUnreachableAgentAsync("r1", It.IsAny<CancellationToken>()))
            .Callback(() => released = true)
            .ReturnsAsync(reservation);

        var service = CreateService(interactionManager, callSessionManager, new Mock<ITelephonyService>(MockBehavior.Strict), reservationManager, reservationService);

        // Act
        var failed = await service.FailAsync("Telnyx", "call-1", HangupCause.NoAnswer, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(failed);
        interactionManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        callSessionManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FailAsync_WhenTheFailureIsDeliveredAgainAfterTheCallerWasReleased_DoesNothing()
    {
        // Arrange
        // The first delivery released the caller: the reservation is settled, the interaction was re-offered with no
        // agent, and the caller is in the queue or leaving a message. A redelivery must not release them a second time,
        // and must not fall back to settling the call, which would hang up on them.
        var interaction = CreateQueuedInboundInteraction("q1");
        interaction.AgentId = null;
        var session = CreateSessionWithDialingAgentLeg();
        CallTopologyProjector.EndLeg(session, "agent-leg-1", _now.AddSeconds(-1), HangupCause.NoAnswer);
        session.AgentId = null;

        var interactionManager = CreateInteractionManager(interaction);
        var callSessionManager = CreateCallSessionManager(session);
        var reservationManager = new Mock<IActivityReservationManager>(MockBehavior.Strict);
        reservationManager
            .Setup(manager => manager.GetActiveByActivityAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var reservationService = new Mock<IActivityReservationService>(MockBehavior.Strict);
        var telephonyService = new Mock<ITelephonyService>(MockBehavior.Strict);

        var service = CreateService(interactionManager, callSessionManager, telephonyService, reservationManager, reservationService);

        // Act
        var failed = await service.FailAsync("Telnyx", "call-1", HangupCause.NoAnswer, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(failed);
        Assert.Equal(InteractionStatus.Ringing, interaction.Status);
        Assert.Equal(VoiceCallState.Ringing, session.State);
        interactionManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        callSessionManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        telephonyService.Verify(telephony => telephony.HangupAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(_auditRecorder.CallsOf(ContactCenterConstants.Events.AgentLegFailed));
    }

    [Fact]
    public async Task FailAsync_WhenAnotherTransitionHoldsTheOffer_LeavesTheCallerToIt()
    {
        // Arrange
        var interaction = CreateQueuedInboundInteraction("q1");
        var session = CreateSessionWithDialingAgentLeg();
        var reservation = CreateAcceptedReservation("q1");

        var interactionManager = CreateInteractionManager(interaction);
        var callSessionManager = CreateCallSessionManager(session);
        var reservationManager = CreateReservationManager("act-1", reservation);
        var reservationService = new Mock<IActivityReservationService>(MockBehavior.Strict);
        reservationService
            .Setup(service => service.ReleaseUnreachableAgentAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActivityReservation?)null);
        var telephonyService = new Mock<ITelephonyService>(MockBehavior.Strict);

        var service = CreateService(interactionManager, callSessionManager, telephonyService, reservationManager, reservationService);

        // Act
        var failed = await service.FailAsync("Telnyx", "call-1", HangupCause.NoAnswer, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(failed);
        Assert.Equal(InteractionStatus.Ringing, interaction.Status);
        telephonyService.Verify(telephony => telephony.HangupAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(InteractionDirection.Outbound, "__campaign-queue__campaign-1")]
    [InlineData(InteractionDirection.Outbound, "q1")]
    [InlineData(InteractionDirection.Inbound, null)]
    public async Task FailAsync_WhenTheCallIsNotAQueuedInboundCaller_SettlesTheCallAsBefore(InteractionDirection direction, string? queueId)
    {
        // Arrange
        // A dialer campaign call, a queued callback and a call that waits in no queue keep the failure path they had:
        // the call is settled as failed, the customer released, and the agent returned to work.
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "Telnyx",
            ProviderInteractionId = "call-1",
            ActivityItemId = "act-1",
            QueueId = queueId,
            AgentId = "a1",
            Direction = direction,
            AnsweredUtc = _now.AddSeconds(-5),
        }.RestorePersistedStatus(InteractionStatus.Connected);

        var interactionManager = CreateInteractionManager(interaction);
        var callSessionManager = new Mock<ICallSessionManager>();
        var telephonyService = new Mock<ITelephonyService>();
        telephonyService
            .Setup(service => service.HangupAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success(new TelephonyCall { CallId = "call-1" }));
        var presenceManager = new Mock<IAgentPresenceManager>();

        var service = CreateService(
            interactionManager,
            callSessionManager,
            telephonyService,
            new Mock<IActivityReservationManager>(MockBehavior.Strict),
            new Mock<IActivityReservationService>(MockBehavior.Strict),
            presenceManager);

        // Act
        var failed = await service.FailAsync("Telnyx", "call-1", HangupCause.NoAnswer, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(failed);
        Assert.Equal(InteractionStatus.Failed, interaction.Status);
        Assert.Equal(_now, interaction.EndedUtc);
        telephonyService.Verify(
            telephony => telephony.HangupAsync(It.Is<CallReference>(call => call.CallId == "call-1"), It.IsAny<CancellationToken>()),
            Times.Once);
        presenceManager.Verify(
            manager => manager.CompleteWorkAsync("a1", It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FailAsync_WhenTheCallIsOnAPersonalLine_SettlesTheCallAsBefore()
    {
        // Arrange
        // A direct-to-agent call has no queue to decide for the caller, so it keeps the failure path it had.
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "Telnyx",
            ProviderInteractionId = "call-1",
            ActivityItemId = "act-1",
            QueueId = ContactCenterConstants.DirectRouting.QueueId,
            AgentId = "a1",
            Direction = InteractionDirection.Inbound,
        }.RestorePersistedStatus(InteractionStatus.Ringing);

        var interactionManager = CreateInteractionManager(interaction);
        var telephonyService = new Mock<ITelephonyService>();
        telephonyService
            .Setup(service => service.HangupAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success(new TelephonyCall { CallId = "call-1" }));

        var service = CreateService(
            interactionManager,
            new Mock<ICallSessionManager>(),
            telephonyService,
            new Mock<IActivityReservationManager>(MockBehavior.Strict),
            new Mock<IActivityReservationService>(MockBehavior.Strict));

        // Act
        var failed = await service.FailAsync("Telnyx", "call-1", HangupCause.NoAnswer, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(failed);
        Assert.Equal(InteractionStatus.Failed, interaction.Status);
        telephonyService.Verify(
            telephony => telephony.HangupAsync(It.Is<CallReference>(call => call.CallId == "call-1"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private ContactCenterAgentLegFailureService CreateService(
        Mock<IInteractionManager> interactionManager,
        Mock<ICallSessionManager> callSessionManager,
        Mock<ITelephonyService> telephonyService,
        Mock<IActivityReservationManager> reservationManager,
        Mock<IActivityReservationService> reservationService,
        Mock<IAgentPresenceManager>? presenceManager = null,
        Mock<IDialerAbandonmentTracker>? abandonmentTracker = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new ContactCenterAgentLegFailureService(
            interactionManager.Object,
            callSessionManager.Object,
            telephonyService.Object,
            _auditRecorder,
            new Mock<IProviderVoiceEventService>(MockBehavior.Strict).Object,
            clock.Object,
            NullLogger<ContactCenterAgentLegFailureService>.Instance,
            (presenceManager ?? new Mock<IAgentPresenceManager>()).Object,
            (abandonmentTracker ?? new Mock<IDialerAbandonmentTracker>()).Object,
            reservationManager.Object,
            reservationService.Object);
    }

    private static Interaction CreateQueuedInboundInteraction(string queueId)
        => new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "Telnyx",
            ProviderInteractionId = "call-1",
            ActivityItemId = "act-1",
            QueueId = queueId,
            AgentId = "a1",
            Direction = InteractionDirection.Inbound,
        }.RestorePersistedStatus(InteractionStatus.Ringing);

    // The caller's leg is up and on the bridge; the agent's accept-time leg is still dialing when it is refused.
    private static CallSession CreateSessionWithDialingAgentLeg()
    {
        var session = new CallSession
        {
            ItemId = "call-session-1",
            InteractionId = "interaction-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-1",
            AgentId = "a1",
            QueueId = "q1",
            Direction = InteractionDirection.Inbound,
        }.RestorePersistedState(VoiceCallState.Ringing);

        CallTopologyProjector.UpsertLeg(session, "call-1", CallPartyRole.Customer, CallLegStatus.Answered, _now.AddSeconds(-30));
        CallTopologyProjector.EnsureBridge(session, "bridge-1", _now.AddSeconds(-30));
        CallTopologyProjector.Join(session, "call-1", CallPartyRole.Customer, _now.AddSeconds(-30));
        CallTopologyProjector.UpsertLeg(session, "agent-leg-1", CallPartyRole.Agent, CallLegStatus.Dialing, _now.AddSeconds(-3), agentId: "a1");

        return session;
    }

    private static ActivityReservation CreateAcceptedReservation(string queueId)
        => new ActivityReservation
        {
            ItemId = "r1",
            QueueId = queueId,
            QueueItemId = "qi-1",
            AgentId = "a1",
            ActivityItemId = "act-1",
            CreatedUtc = _now.AddSeconds(-20),
        }.RestorePersistedStatus(ReservationStatus.Accepted);

    private static Mock<IInteractionManager> CreateInteractionManager(Interaction interaction)
    {
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("Telnyx", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        return interactionManager;
    }

    private static Mock<ICallSessionManager> CreateCallSessionManager(CallSession session)
    {
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        return callSessionManager;
    }

    private static Mock<IActivityReservationManager> CreateReservationManager(string activityItemId, ActivityReservation reservation)
    {
        var reservationManager = new Mock<IActivityReservationManager>(MockBehavior.Strict);
        reservationManager
            .Setup(manager => manager.GetActiveByActivityAsync(activityItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([reservation]);

        return reservationManager;
    }
}
