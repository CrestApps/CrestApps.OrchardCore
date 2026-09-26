using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// A supervisor on an agent's own Telnyx phone call. A number dialed from the keypad runs as the agent's leg bridged to
/// the number's with <c>park_after_unbridge=self</c> -- the shape of a Contact Center call -- so it is moved into a
/// conference of its own the same way, from the number's leg. An extension call already runs in the conference made from
/// the caller's own leg, with the colleague joined to it: the supervisor joins that one as it is, and no new conference is
/// ever made from a leg that has to outlive it.
/// </summary>
public sealed class TelnyxPhoneCallMonitoringTests
{
    private const string AgentLeg = "agent-leg";
    private const string NumberLeg = "number-leg";
    private const string ColleagueLeg = "colleague-leg";
    private const string Supervisor = "supervisor-leg";
    private const string KeypadConference = "cc-sv-number-leg";
    private const string ExtensionConference = "ext-agent-leg";

    [Fact]
    public async Task ANumberDialedFromTheKeypad_IsSupervisedLikeAContactCenterCall_FromTheNumbersLeg()
    {
        // Arrange
        var api = KeypadCall();
        var provider = TelnyxContactCenterProviderFactory.Create(api);

        // Act
        var target = await provider.ResolvePhoneCallAsync(AgentLeg, isExtension: false, monitorsCallee: false, TestContext.Current.CancellationToken);

        // Assert - read only: nothing is moved until the supervisor's phone answers.
        Assert.Equal([$"GET calls/{AgentLeg}", $"GET calls/{NumberLeg}"], api.Commands);
        Assert.Equal(NumberLeg, target.ProviderCallId);
        Assert.Equal(AgentLeg, target.AgentLegId);
        Assert.Equal(NumberLeg, target.OtherPartyLegId);
        Assert.Equal(KeypadConference, target.ConferenceName);
        Assert.True(target.CanTakeOver);
    }

    [Theory]
    [InlineData(false, AgentLeg, ColleagueLeg)]
    [InlineData(true, ColleagueLeg, AgentLeg)]
    public async Task AnExtensionCall_IsSupervisedInItsOwnConference_WhicheverColleagueIsMonitored(bool monitorsCallee, string monitored, string other)
    {
        // Arrange
        var api = ExtensionCall();
        var provider = TelnyxContactCenterProviderFactory.Create(api);

        // Act
        var target = await provider.ResolvePhoneCallAsync(AgentLeg, isExtension: true, monitorsCallee, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([$"GET calls/{AgentLeg}", "GET conferences"], api.Commands);
        Assert.Equal(AgentLeg, target.ProviderCallId);
        Assert.Equal(monitored, target.AgentLegId);
        Assert.Equal(other, target.OtherPartyLegId);
        Assert.Equal(ExtensionConference, target.ConferenceName);

        // Releasing either colleague ends an extension call: it cannot be taken over.
        Assert.False(target.CanTakeOver);
    }

    [Fact]
    public async Task ACallThatEnded_ACallMovedElsewhere_OrAnExtensionNotConnectedYet_CannotBeMonitored()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var ended = KeypadCall();
        ended.HungUp.Add(AgentLeg);
        Assert.Null(await TelnyxContactCenterProviderFactory.Create(ended).ResolvePhoneCallAsync(AgentLeg, false, false, cancellationToken));

        var numberGone = KeypadCall();
        numberGone.HungUp.Add(NumberLeg);
        Assert.Null(await TelnyxContactCenterProviderFactory.Create(numberGone).ResolvePhoneCallAsync(AgentLeg, false, false, cancellationToken));

        // A transfer or a merge detaches the agent's leg from the call it placed.
        var moved = new FakeTelnyxCallControl().WithLeg(AgentLeg, KeypadAgentState().AsDetached()).WithLeg(NumberLeg, null);
        Assert.Null(await TelnyxContactCenterProviderFactory.Create(moved).ResolvePhoneCallAsync(AgentLeg, false, false, cancellationToken));

        // Still ringing: the number has not answered.
        var ringingNumber = new FakeTelnyxCallControl().WithLeg(AgentLeg, new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.AgentLegIntent,
            PeerCallControlId = NumberLeg,
            PeerAnswered = false,
        }).WithLeg(NumberLeg, KeypadNumberState());
        Assert.Null(await TelnyxContactCenterProviderFactory.Create(ringingNumber).ResolvePhoneCallAsync(AgentLeg, false, false, cancellationToken));

        // Still dialing: the number's leg is not known yet.
        var dialing = new FakeTelnyxCallControl().WithLeg(AgentLeg, new TelnyxOutboundBridgeState { Intent = TelnyxOutboundBridgeState.AgentLegIntent });
        Assert.Null(await TelnyxContactCenterProviderFactory.Create(dialing).ResolvePhoneCallAsync(AgentLeg, false, false, cancellationToken));

        var ringing = new FakeTelnyxCallControl().WithLeg(AgentLeg, ExtensionAgentState());
        Assert.Null(await TelnyxContactCenterProviderFactory.Create(ringing).ResolvePhoneCallAsync(AgentLeg, true, false, cancellationToken));

        // What the call is must match what it says it is.
        Assert.Null(await TelnyxContactCenterProviderFactory.Create(KeypadCall()).ResolvePhoneCallAsync(AgentLeg, true, false, cancellationToken));
        Assert.Null(await TelnyxContactCenterProviderFactory.Create(ExtensionCall()).ResolvePhoneCallAsync(AgentLeg, false, false, cancellationToken));
    }

    [Fact]
    public async Task Engage_OnAnExtensionCall_RingsTheSupervisor_WithALegThatNamesTheCallsOwnConference()
    {
        // Arrange
        var api = ExtensionCall();
        api.NextLegId = Supervisor;
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());

        // Act
        var result = await provider.EngageAsync(ExtensionRequest(MonitorMode.Whisper), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(["POST calls"], api.Commands);

        var state = FakeTelnyxCallControl.StateIn(api.BodyOf("POST", "calls"));
        Assert.Equal(TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent, state.Intent);
        Assert.Equal(ExtensionConference, state.ConferenceName);
        Assert.Equal(AgentLeg, state.PeerCallControlId);
        Assert.Equal(ColleagueLeg, state.PartyCallControlId);
        Assert.Equal("whisper", state.SupervisorRole);
    }

    [Theory]
    [InlineData("monitor", null, true)]
    [InlineData("whisper", "whisper", false)]
    [InlineData("barge", "barge", false)]
    public async Task SupervisorAnswers_OnAnExtensionCall_JoinsItsConferenceAsItIs_WithoutMovingAnybody(string role, string joinedRole, bool muted)
    {
        // Arrange
        var api = ExtensionCall();
        var state = ExtensionSupervisorState(role);
        api.WithLeg(Supervisor, state);
        var sink = new Mock<ISupervisorLegEventSink>();
        var orchestrator = TelnyxSupervisorMonitoringTests.CreateOrchestrator(api, sink.Object);

        // Act
        await orchestrator.AdvanceAsync(TelnyxSupervisorMonitoringTests.Answered(Supervisor, state), TestContext.Current.CancellationToken);

        // Assert - no conference is made from either colleague's leg, and nobody leaves the one they are in.
        Assert.Equal(["GET conferences", "POST conferences/conf-1/actions/join"], api.Commands);

        var join = api.BodyOf("POST", "conferences/conf-1/actions/join");
        Assert.Equal(Supervisor, join.GetProperty("call_control_id").GetString());
        Assert.Equal(joinedRole, join.TryGetProperty("supervisor_role", out var joinedAs) ? joinedAs.GetString() : null);
        Assert.Equal(muted, join.TryGetProperty("mute", out var mute) && mute.GetBoolean());
        Assert.False(join.TryGetProperty("end_conference_on_exit", out _));

        if (joinedRole == "whisper")
        {
            Assert.Equal([ColleagueLeg], join.GetProperty("whisper_call_control_ids").EnumerateArray().Select(item => item.GetString()));
        }

        Assert.Equal([AgentLeg, ColleagueLeg, Supervisor], api.Conferences["conf-1"].Participants);
        Assert.Empty(api.HungUp);
        sink.Verify(value => value.OnAnsweredAsync(TelnyxConstants.ProviderTechnicalName, AgentLeg, Supervisor, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ASupervisorLeg_IsReportedToTheFirstSinkThatKnowsIt_AContactCenterCallsFirst_ThenAPhoneCalls(bool contactCenterKnowsIt)
    {
        // Arrange
        var api = ExtensionCall();
        var state = ExtensionSupervisorState("monitor");
        api.WithLeg(Supervisor, state);
        var contactCenter = new Mock<ISupervisorLegEventSink>();
        contactCenter.Setup(value => value.OnAnsweredAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(contactCenterKnowsIt);
        contactCenter.Setup(value => value.OnEndedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CrestApps.OrchardCore.Telephony.Models.HangupCause?>(), It.IsAny<CancellationToken>())).ReturnsAsync(contactCenterKnowsIt);
        var phoneCalls = new Mock<ISupervisorLegEventSink>();
        var orchestrator = TelnyxSupervisorMonitoringTests.CreateOrchestrator(api, contactCenter.Object, nextSink: phoneCalls.Object);

        // Act
        await orchestrator.AdvanceAsync(TelnyxSupervisorMonitoringTests.Answered(Supervisor, state), TestContext.Current.CancellationToken);
        await orchestrator.AdvanceAsync(TelnyxSupervisorMonitoringTests.Hangup(Supervisor, state), TestContext.Current.CancellationToken);

        // Assert
        var times = contactCenterKnowsIt ? Times.Never() : Times.Once();
        phoneCalls.Verify(value => value.OnAnsweredAsync(TelnyxConstants.ProviderTechnicalName, AgentLeg, Supervisor, It.IsAny<CancellationToken>()), times);
        phoneCalls.Verify(value => value.OnEndedAsync(TelnyxConstants.ProviderTechnicalName, AgentLeg, Supervisor, It.IsAny<DateTime?>(), It.IsAny<CrestApps.OrchardCore.Telephony.Models.HangupCause?>(), It.IsAny<CancellationToken>()), times);
    }

    [Fact]
    public async Task SupervisorAnswers_OnAKeypadCall_TheNumbersLegMakesTheConference_AndTheAgentsLegJoinsIt()
    {
        // Arrange
        var api = KeypadCall();
        var state = KeypadSupervisorState("barge");
        api.WithLeg(Supervisor, state);
        var orchestrator = TelnyxSupervisorMonitoringTests.CreateOrchestrator(api, Mock.Of<ISupervisorLegEventSink>());

        // Act
        await orchestrator.AdvanceAsync(TelnyxSupervisorMonitoringTests.Answered(Supervisor, state), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["GET conferences", "POST conferences", "POST conferences/conf-1/actions/join", "POST conferences/conf-1/actions/join"], api.Commands);
        Assert.Equal(NumberLeg, api.BodyOf("POST", "conferences").GetProperty("call_control_id").GetString());
        Assert.Equal([NumberLeg, AgentLeg, Supervisor], api.Conferences["conf-1"].Participants);
        Assert.Empty(api.HungUp);
    }

    [Fact]
    public async Task SwitchMode_OnAnExtensionCall_ChangesTheRoleInTheCallsOwnConference()
    {
        // Arrange
        var api = ExtensionCall();
        api.Conferences["conf-1"].Participants.Add(Supervisor);
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = ExtensionRequest(MonitorMode.Barge);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.SwitchModeAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(["GET conferences", "POST conferences/conf-1/actions/update", "POST conferences/conf-1/actions/unmute"], api.Commands);
        Assert.Contains(ExtensionConference, api.Requests[0].Query, StringComparison.Ordinal);
        Assert.Equal("barge", api.BodyOf("POST", "conferences/conf-1/actions/update").GetProperty("supervisor_role").GetString());
    }

    [Fact]
    public async Task Stop_OnAnExtensionCall_HangsUpOnlyTheSupervisor_AndMovesNobody()
    {
        // Arrange
        var api = ExtensionCall();
        api.Conferences["conf-1"].Participants.Add(Supervisor);
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = ExtensionRequest(MonitorMode.Monitor);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.StopAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal([Supervisor], api.HungUp);
        Assert.Empty(api.Bridges);
        Assert.Equal([AgentLeg, ColleagueLeg], api.Conferences["conf-1"].Participants);
        Assert.DoesNotContain(api.Commands, command => command.StartsWith("POST conferences/conf-1/actions/leave", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TakeOver_OfAKeypadCall_ReleasesTheAgent_AndTheNumbersEndNowEndsTheSupervisorsCall()
    {
        // Arrange - the call is in its supervised conference, the supervisor on it.
        var api = KeypadCall();
        api.WithConference(KeypadConference, NumberLeg, AgentLeg, Supervisor);
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = KeypadRequest(MonitorMode.Barge);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.TakeOverPhoneCallAsync(request, TestContext.Current.CancellationToken);

        // Assert - heard by the number first, then the agent is let go, marked so its end does not end the call.
        Assert.True(result.Succeeded);
        Assert.Equal(
            [
                "GET conferences",
                "POST conferences/conf-1/actions/update",
                "POST conferences/conf-1/actions/unmute",
                $"GET calls/{AgentLeg}",
                $"POST calls/{AgentLeg}/actions/hangup",
                $"GET calls/{NumberLeg}",
                $"PUT calls/{NumberLeg}/actions/client_state_update",
            ],
            api.Commands);
        Assert.Equal([AgentLeg], api.HungUp);
        Assert.True(TelnyxOutboundBridgeState.TryParse(api.LegStates[AgentLeg], out var released));
        Assert.True(released.Detached);
        Assert.True(TelnyxOutboundBridgeState.TryParse(api.LegStates[NumberLeg], out var number));
        Assert.Equal(Supervisor, number.PeerCallControlId);
        Assert.Equal([NumberLeg, Supervisor], api.Conferences["conf-1"].Participants);
    }

    [Fact]
    public async Task AfterATakeover_TheNumberHangingUp_HangsUpTheSupervisor_NotAnybodyElse()
    {
        // Arrange - the number's leg now names the supervisor's leg.
        var api = KeypadCall();
        var numberState = KeypadNumberState().WithPeer(Supervisor);
        api.WithLeg(NumberLeg, numberState);
        var orchestrator = TelnyxSupervisorMonitoringTests.CreateOrchestrator(api, Mock.Of<ISupervisorLegEventSink>());

        // Act
        await orchestrator.AdvanceAsync(TelnyxSupervisorMonitoringTests.Hangup(NumberLeg, numberState), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(Supervisor, api.HungUp);
        Assert.DoesNotContain(AgentLeg, api.HungUp);
    }

    [Fact]
    public async Task HangingUpTheOtherPartyOfACallTheSupervisorTookOver_IsHarmlessWhenItIsAlreadyGone()
    {
        // Arrange
        var api = KeypadCall();
        api.HungUp.Add(NumberLeg);
        var provider = TelnyxContactCenterProviderFactory.Create(api);

        // Act
        await provider.HangupPhoneCallLegAsync(NumberLeg, TestContext.Current.CancellationToken);
        await provider.HangupPhoneCallLegAsync(null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([$"POST calls/{NumberLeg}/actions/hangup"], api.Commands);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void WhatTheDashboardOffers_IsExtensionCallsAndNumbersTheAgentDialed(bool isExtension, bool isOutbound, bool expected)
        => Assert.Equal(expected, TelnyxContactCenterProviderFactory.Create(new FakeTelnyxCallControl()).CanMonitorPhoneCall(isExtension, isOutbound));

    private static FakeTelnyxCallControl KeypadCall()
        => new FakeTelnyxCallControl()
            .WithLeg(AgentLeg, KeypadAgentState())
            .WithLeg(NumberLeg, KeypadNumberState());

    private static FakeTelnyxCallControl ExtensionCall()
    {
        var api = new FakeTelnyxCallControl()
            .WithLeg(AgentLeg, ExtensionAgentState())
            .WithLeg(ColleagueLeg, new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
                PeerCallControlId = AgentLeg,
                VoicemailRecipientUserId = "colleague-user",
            });

        // Made from the caller's own leg; the colleague joined it.
        api.WithConference(ExtensionConference, AgentLeg, ColleagueLeg);

        return api;
    }

    private static TelnyxOutboundBridgeState KeypadAgentState()
        => new()
        {
            Intent = TelnyxOutboundBridgeState.AgentLegIntent,
            Destination = "+17025550100",
            PeerCallControlId = NumberLeg,
            PeerAnswered = true,
        };

    private static TelnyxOutboundBridgeState KeypadNumberState()
        => new()
        {
            Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
            PeerCallControlId = AgentLeg,
        };

    private static TelnyxOutboundBridgeState ExtensionAgentState()
        => new()
        {
            Intent = TelnyxOutboundBridgeState.AgentLegIntent,
            VoicemailRecipientUserId = "colleague-user",
            PeerCallControlId = ColleagueLeg,
        };

    private static TelnyxOutboundBridgeState ExtensionSupervisorState(string role)
        => new()
        {
            Intent = TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent,
            PeerCallControlId = AgentLeg,
            PartyCallControlId = ColleagueLeg,
            ConferenceName = ExtensionConference,
            SupervisorRole = role,
            RingUserId = "supervisor-user",
            MonitorToken = "token-1",
        };

    private static TelnyxOutboundBridgeState KeypadSupervisorState(string role)
        => new()
        {
            Intent = TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent,
            PeerCallControlId = NumberLeg,
            PartyCallControlId = AgentLeg,
            ConferenceName = KeypadConference,
            SupervisorRole = role,
            RingUserId = "supervisor-user",
            MonitorToken = "token-1",
        };

    private static ContactCenterVoiceMonitoringRequest ExtensionRequest(MonitorMode mode)
    {
        var request = new ContactCenterVoiceMonitoringRequest
        {
            InteractionId = "phone:colleague-user:agent-leg",
            ProviderCallId = AgentLeg,
            SupervisorId = "supervisor-user",
            Mode = mode,
            AgentLegId = ColleagueLeg,
            MonitorToken = "token-1",
        };

        request.Metadata[ContactCenterPhoneCallMonitoringTarget.ConferenceMetadataKey] = ExtensionConference;

        return request;
    }

    private static ContactCenterVoiceMonitoringRequest KeypadRequest(MonitorMode mode)
    {
        var request = new ContactCenterVoiceMonitoringRequest
        {
            InteractionId = "phone:agent-user:agent-leg",
            ProviderCallId = NumberLeg,
            SupervisorId = "supervisor-user",
            Mode = mode,
            AgentLegId = AgentLeg,
            MonitorToken = "token-1",
        };

        request.Metadata[ContactCenterPhoneCallMonitoringTarget.ConferenceMetadataKey] = KeypadConference;

        return request;
    }

    private static ITelnyxAgentEndpointResolver Resolver()
    {
        var resolver = new Mock<ITelnyxAgentEndpointResolver>();
        resolver
            .Setup(value => value.ResolveAsync("supervisor-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync("sip:gencredSupervisor@sip.telnyx.com");

        return resolver.Object;
    }
}
