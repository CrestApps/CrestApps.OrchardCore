#nullable enable annotations

using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterAgentLegFailureTests
{
    private static readonly DateTime _now = new(2026, 8, 28, 20, 9, 8, DateTimeKind.Utc);

    [Fact]
    public async Task FailAsync_WhenTheAgentLegNeverReachedTheAgent_SettlesTheCallAndReleasesTheCustomer()
    {
        // Arrange
        // The agent leg is originated on its own provider identifier, which belongs to no interaction, so its
        // failure is discarded by normalization and nothing ends the call. The customer is left connected to an
        // agent who was never reached, the agent is left "on a call" and is offered no further work, and
        // recovery skips the record because recovery never touches an unsettled interaction.
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "Telnyx",
            ProviderInteractionId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Outbound,
            AnsweredUtc = _now.AddSeconds(-5),
        }.RestorePersistedStatus(InteractionStatus.Connected);

        var session = new CallSession
        {
            ItemId = "call-session-1",
            InteractionId = "interaction-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Outbound,
            AnsweredUtc = _now.AddSeconds(-5),
        }.RestorePersistedState(VoiceCallState.Connected);

        CallTopologyProjector.UpsertLeg(session, "call-1", CallPartyRole.Customer, CallLegStatus.Answered, _now.AddSeconds(-5));
        CallTopologyProjector.EnsureBridge(session, "bridge-1", _now.AddSeconds(-5));
        CallTopologyProjector.Join(session, "call-1", CallPartyRole.Customer, _now.AddSeconds(-5));

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("Telnyx", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var telephonyService = new Mock<ITelephonyService>();
        telephonyService
            .Setup(service => service.HangupAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success(new TelephonyCall { CallId = "call-1" }));

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var service = new ContactCenterAgentLegFailureService(
            interactionManager.Object,
            callSessionManager.Object,
            telephonyService.Object,
            clock.Object,
            NullLogger<ContactCenterAgentLegFailureService>.Instance);

        // Act
        var failed = await service.FailAsync("Telnyx", "call-1", HangupCause.Rejected, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(failed);
        Assert.Equal(InteractionStatus.Failed, interaction.Status);
        Assert.Equal(_now, interaction.EndedUtc);
        Assert.Equal(VoiceCallState.Ended, session.State);
        Assert.Equal(_now, session.EndedUtc);
        Assert.All(session.Legs, leg => Assert.True(leg.EndedUtc.HasValue, "A leg was left open."));
        Assert.Empty(session.Bridge.ActiveParticipants);

        telephonyService.Verify(
            telephony => telephony.HangupAsync(It.Is<CallReference>(call => call.CallId == "call-1"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FailAsync_WhenTheInteractionAlreadySettled_LeavesTheRecordedOutcomeAlone()
    {
        // Arrange
        // The agent leg of a call that ended normally also terminates. Treating that as a connect failure would
        // overwrite the real ending with an artifact of the teardown and hang up a call that already finished.
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "Telnyx",
            ProviderInteractionId = "call-1",
            EndedUtc = _now.AddMinutes(-1),
        }.RestorePersistedStatus(InteractionStatus.Ended);

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("Telnyx", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var telephonyService = new Mock<ITelephonyService>(MockBehavior.Strict);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var service = new ContactCenterAgentLegFailureService(
            interactionManager.Object,
            new Mock<ICallSessionManager>(MockBehavior.Strict).Object,
            telephonyService.Object,
            clock.Object,
            NullLogger<ContactCenterAgentLegFailureService>.Instance);

        // Act
        var failed = await service.FailAsync("Telnyx", "call-1", HangupCause.Rejected, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(failed);
        Assert.Equal(InteractionStatus.Ended, interaction.Status);
        Assert.Equal(_now.AddMinutes(-1), interaction.EndedUtc);

        telephonyService.Verify(
            telephony => telephony.HangupAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AdvanceAsync_WhenTheContactCenterAgentLegIsRejected_ReportsTheFailureAgainstThePeerCall()
    {
        // Arrange
        // The rejected agent leg carries its own identifier, which matches no interaction, so this is the only
        // point that still knows which call the leg was being connected to -- the peer id in its client_state.
        var failureService = new Mock<IContactCenterAgentLegFailureService>();
        failureService
            .Setup(service => service.FailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<HangupCause?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var orchestrator = new TelnyxOutboundBridgeOrchestrator(
            new Mock<IHttpClientFactory>().Object,
            NullLogger<TelnyxOutboundBridgeOrchestrator>.Instance,
            CreateMonitor(),
            failureService.Object);

        // The webhook parser base64-decodes client_state before the orchestrator sees it, so the event carries
        // decoded JSON.
        var clientState = DecodeClientState(new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.ContactCenterAgentLegIntent,
            PeerCallControlId = "call-1",
        }.ToClientState());

        // Act
        var leg = await orchestrator.AdvanceAsync(new TelnyxCallEvent
        {
            EventType = "call.hangup",
            CallControlId = "agent-leg-1",
            HangupCause = "CALL_REJECTED",
            ClientState = clientState,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelnyxOutboundBridgeLeg.None, leg);

        failureService.Verify(
            service => service.FailAsync("Telnyx", "call-1", HangupCause.Rejected, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AdvanceAsync_WhenTheContactCenterAgentLegEndsNormally_DoesNotReportAConnectFailure()
    {
        // Arrange
        // A normal clearing is the agent leg of a real conversation ending, not a leg that never reached the
        // agent. Reporting it would settle a finished call as failed and hang up a call that already ended.
        var failureService = new Mock<IContactCenterAgentLegFailureService>(MockBehavior.Strict);

        var orchestrator = new TelnyxOutboundBridgeOrchestrator(
            new Mock<IHttpClientFactory>().Object,
            NullLogger<TelnyxOutboundBridgeOrchestrator>.Instance,
            CreateMonitor(),
            failureService.Object);

        // The webhook parser base64-decodes client_state before the orchestrator sees it, so the event carries
        // decoded JSON.
        var clientState = DecodeClientState(new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.ContactCenterAgentLegIntent,
            PeerCallControlId = "call-1",
        }.ToClientState());

        // Act
        var leg = await orchestrator.AdvanceAsync(new TelnyxCallEvent
        {
            EventType = "call.hangup",
            CallControlId = "agent-leg-1",
            HangupCause = "NORMAL_CLEARING",
            ClientState = clientState,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelnyxOutboundBridgeLeg.None, leg);

        failureService.Verify(
            service => service.FailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<HangupCause?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RecordAnsweredAsync_WhenTheAgentLegAnswers_AdvancesTheAgentLegToAnswered()
    {
        // Arrange
        // The platform records the agent leg on the call topology at dialing when it originates it, but the
        // agent leg's own call.answered is keyed by the agent-leg id, which belongs to no interaction, so it is
        // discarded by normalization. Left unadvanced the leg is later marked failed with no answered time,
        // misreporting who was on the call. The peer id in the leg's client_state is the customer call it joined.
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderName = "Telnyx",
            ProviderInteractionId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Outbound,
            AnsweredUtc = _now.AddSeconds(-5),
        }.RestorePersistedStatus(InteractionStatus.Connected);

        var session = new CallSession
        {
            ItemId = "call-session-1",
            InteractionId = "interaction-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            Direction = InteractionDirection.Outbound,
            AnsweredUtc = _now.AddSeconds(-5),
        }.RestorePersistedState(VoiceCallState.Connected);

        CallTopologyProjector.UpsertLeg(session, "call-1", CallPartyRole.Customer, CallLegStatus.Answered, _now.AddSeconds(-5));
        CallTopologyProjector.EnsureBridge(session, "bridge-1", _now.AddSeconds(-5));
        CallTopologyProjector.Join(session, "call-1", CallPartyRole.Customer, _now.AddSeconds(-5));

        // The agent leg as the connect command left it: recorded, but only dialing and not yet answered.
        CallTopologyProjector.UpsertLeg(session, "agent-leg-1", CallPartyRole.Agent, CallLegStatus.Dialing, _now.AddSeconds(-3), agentId: "agent-1");

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("Telnyx", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var service = new ContactCenterAgentLegFailureService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<ITelephonyService>(MockBehavior.Strict).Object,
            clock.Object,
            NullLogger<ContactCenterAgentLegFailureService>.Instance);

        // Act
        var advanced = await service.RecordAnsweredAsync("Telnyx", "call-1", "agent-leg-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(advanced);

        var agentLeg = Assert.Single(session.Legs, leg => leg.ProviderLegId == "agent-leg-1");
        Assert.Equal(CallLegStatus.Answered, agentLeg.Status);
        Assert.Equal(CallPartyRole.Agent, agentLeg.Role);
        Assert.Equal(_now, agentLeg.AnsweredUtc);
        Assert.Null(agentLeg.EndedUtc);

        // The agent is a party on the bridge, so the call correctly reports who was on it.
        Assert.Contains(session.Bridge.ActiveParticipants, participant => participant.ProviderLegId == "agent-leg-1");

        callSessionManager.Verify(
            manager => manager.UpdateAsync(session, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AdvanceAsync_WhenTheContactCenterAgentLegAnswers_RecordsTheAnswerAgainstThePeerCall()
    {
        // Arrange
        // The answered agent leg carries its own identifier, which matches no interaction, so this is the point
        // that still knows which customer call the leg joined -- the peer id in its client_state.
        var failureService = new Mock<IContactCenterAgentLegFailureService>();
        failureService
            .Setup(service => service.RecordAnsweredAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var orchestrator = new TelnyxOutboundBridgeOrchestrator(
            new Mock<IHttpClientFactory>().Object,
            NullLogger<TelnyxOutboundBridgeOrchestrator>.Instance,
            CreateMonitor(),
            failureService.Object);

        var clientState = DecodeClientState(new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.ContactCenterAgentLegIntent,
            PeerCallControlId = "call-1",
        }.ToClientState());

        // Act
        var leg = await orchestrator.AdvanceAsync(new TelnyxCallEvent
        {
            EventType = "call.answered",
            CallControlId = "agent-leg-1",
            ClientState = clientState,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelnyxOutboundBridgeLeg.None, leg);

        failureService.Verify(
            service => service.RecordAnsweredAsync("Telnyx", "call-1", "agent-leg-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static string DecodeClientState(string clientState)
        => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(clientState));

    private static IOptionsMonitor<TelnyxOptions> CreateMonitor()
    {
        var monitor = new Mock<IOptionsMonitor<TelnyxOptions>>();
        monitor.SetupGet(value => value.CurrentValue).Returns(new TelnyxOptions());

        return monitor.Object;
    }
}
