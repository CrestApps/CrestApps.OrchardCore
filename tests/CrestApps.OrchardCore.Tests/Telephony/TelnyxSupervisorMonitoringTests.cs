using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Supervisor listen, whisper, barge and takeover on Telnyx. A Contact Center call runs as the agent's leg bridged to
/// the customer's with <c>park_after_unbridge=self</c>, and it stays that way: the supervisor's own soft phone is rung
/// with a leg that supervises the agent's (<c>supervise_call_control_id</c> and <c>supervisor_role</c> on the dial), a mode
/// is changed by ringing a fresh leg in the new role (live, <c>switch_supervisor_role</c> left the supervisor unheard or
/// hearing silence), and stopping only hangs it up. Live, moving the call
/// into a conference for a supervisor left the customer and the agent unable to hear each other -- or the supervisor --
/// in every mode, so nobody is moved.
/// </summary>
public sealed class TelnyxSupervisorMonitoringTests
{
    private const string Customer = "customer-leg";
    private const string Agent = "agent-leg";
    private const string Supervisor = "supervisor-leg";
    private const string TakeOverLeg = "takeover-leg";
    private const string ReplacementLeg = "replacement-leg";
    private const string SupervisorEndpoint = "sip:gencredSupervisor@sip.telnyx.com";

    [Theory]
    [InlineData(MonitorMode.Monitor, "monitor")]
    [InlineData(MonitorMode.Whisper, "whisper")]
    [InlineData(MonitorMode.Barge, "barge")]
    public async Task Engage_RingsTheSupervisorsRegisteredPhone_WithALegThatSupervisesTheAgentsLegInTheirRole(MonitorMode mode, string role)
    {
        // Arrange
        var api = new FakeTelnyxCallControl { NextLegId = Supervisor };
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());

        // Act
        var result = await provider.EngageAsync(Request(mode), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(Supervisor, result.ProviderLegId);
        Assert.Equal(VoiceCallState.Dialing, result.ProviderLegState);

        // The one command is the ring to their phone: nothing about the call itself changes.
        Assert.Equal(["POST calls"], api.Commands);

        var dial = api.BodyOf("POST", "calls");
        Assert.Equal(SupervisorEndpoint, dial.GetProperty("to").GetString());
        Assert.Equal("connection-1", dial.GetProperty("connection_id").GetString());
        Assert.False(dial.TryGetProperty("outbound_voice_profile_id", out _));
        Assert.Equal("cc-sv-leg-token-1", dial.GetProperty("command_id").GetString());

        // Telnyx attaches the answered leg to the agent's in the role it is dialed with. Live, a leg dialed to listen was
        // heard; a leg dialed as barge and switched to listen when it answered carried only silence to the supervisor, and
        // a leg switched to barge went silent too. So the leg is dialed in the engagement's own role and never switched.
        Assert.Equal(Agent, dial.GetProperty("supervise_call_control_id").GetString());
        Assert.Equal(role, dial.GetProperty("supervisor_role").GetString());

        var header = Assert.Single(dial.GetProperty("custom_headers").EnumerateArray());
        Assert.Equal(TelnyxConstants.MonitorLegSipHeader, header.GetProperty("name").GetString());
        Assert.Equal("token-1", header.GetProperty("value").GetString());

        var state = FakeTelnyxCallControl.StateIn(dial);
        Assert.Equal(TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent, state.Intent);
        Assert.Equal(Customer, state.PeerCallControlId);
        Assert.Equal(Agent, state.PartyCallControlId);
        Assert.True(state.SupervisesInPlace);
        Assert.Null(state.ConferenceName);
        Assert.Equal(role, state.SupervisorRole);
        Assert.Equal("supervisor-user", state.RingUserId);
        Assert.Equal("token-1", state.MonitorToken);
    }

    [Fact]
    public async Task Engage_WhenTheSupervisorHasNoRegisteredPhone_FailsWithoutRingingAnything()
    {
        // Arrange
        var api = new FakeTelnyxCallControl();
        var resolver = new Mock<ITelnyxAgentEndpointResolver>();
        var provider = TelnyxContactCenterProviderFactory.Create(api, resolver.Object);

        // Act
        var result = await provider.EngageAsync(Request(MonitorMode.Monitor), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("supervisor_endpoint_missing", result.ErrorCode);
        Assert.Empty(api.Requests);
    }

    [Fact]
    public async Task Engage_WithoutTheAgentsLeg_IsRefused()
    {
        // Arrange
        var api = new FakeTelnyxCallControl();
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(MonitorMode.Monitor);
        request.AgentLegId = null;

        // Act
        var result = await provider.EngageAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(api.Requests);
    }

    // Live (2026-09-26), a leg dialed as barge and switched to listen the moment it answered carried nothing but silence to
    // the supervisor, while a leg dialed to listen and left alone was heard. The answered leg keeps the role it was dialed
    // with: no role switch is sent.
    [Theory]
    [InlineData("monitor")]
    [InlineData("whisper")]
    [InlineData("barge")]
    public async Task SupervisorAnswers_ABridgedCall_NobodyIsMoved_AndTheLegKeepsTheRoleItWasDialedWith(string role)
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState(role));
        var sink = new Mock<ISupervisorLegEventSink>();
        var orchestrator = CreateOrchestrator(api, sink.Object);

        // Act
        await orchestrator.AdvanceAsync(Answered(Supervisor, SupervisorState(role)), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(api.Commands);
        Assert.Empty(api.Conferences);
        Assert.Empty(api.Bridges);
        Assert.Empty(api.HungUp);

        sink.Verify(value => value.OnAnsweredAsync(TelnyxConstants.ProviderTechnicalName, Customer, Supervisor, It.IsAny<CancellationToken>()), Times.Once);
    }

    // Live, every accepted switch_supervisor_role left the supervisor unheard, and a switch to barge (or from barge to
    // listen) left them hearing silence. A mode change rings the supervisor's phone with a fresh supervising leg dialed in
    // the new role, carrying the engagement's token so the phone answers it in place of the one it holds, and naming the
    // leg it replaces, which is let go once the new one answers.
    [Theory]
    [InlineData(MonitorMode.Monitor, "monitor")]
    [InlineData(MonitorMode.Whisper, "whisper")]
    [InlineData(MonitorMode.Barge, "barge")]
    public async Task SwitchMode_RingsAFreshSupervisingLegInTheNewRole_AndSendsNoRoleSwitch(MonitorMode mode, string role)
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("monitor"));
        api.NextLegId = ReplacementLeg;
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(mode);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.SwitchModeAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(ReplacementLeg, result.ProviderLegId);
        Assert.Equal(["POST calls"], api.Commands);

        var dial = api.BodyOf("POST", "calls");
        Assert.Equal(SupervisorEndpoint, dial.GetProperty("to").GetString());
        Assert.Equal(Agent, dial.GetProperty("supervise_call_control_id").GetString());
        Assert.Equal(role, dial.GetProperty("supervisor_role").GetString());
        Assert.Equal("token-1", Assert.Single(dial.GetProperty("custom_headers").EnumerateArray()).GetProperty("value").GetString());

        var state = FakeTelnyxCallControl.StateIn(dial);
        Assert.Equal(TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent, state.Intent);
        Assert.Equal(role, state.SupervisorRole);
        Assert.Equal("token-1", state.MonitorToken);
        Assert.Equal(Supervisor, state.ReplacesCallControlId);
        Assert.True(state.SupervisesInPlace);

        // The leg the supervisor is on stays until the new one answers, so they are never cut off in between.
        Assert.Empty(api.HungUp);
        Assert.Empty(api.Conferences);
    }

    // A Contact Center call's request carries no token: it is read from the supervising leg the phone answered.
    [Fact]
    public async Task SwitchMode_WithoutTheToken_ReadsItFromTheSupervisingLeg()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("monitor"));
        api.NextLegId = ReplacementLeg;
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(MonitorMode.Whisper);
        request.SupervisorLegId = Supervisor;
        request.MonitorToken = null;

        // Act
        var result = await provider.SwitchModeAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal([$"GET calls/{Supervisor}", "POST calls"], api.Commands);
        Assert.Equal("token-1", FakeTelnyxCallControl.StateIn(api.BodyOf("POST", "calls")).MonitorToken);
    }

    [Fact]
    public async Task SwitchMode_WhenThePhoneCannotBeRungAgain_FailsAndLeavesTheSupervisorWhereTheyAre()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("monitor"));
        api.RefuseOriginate = true;
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(MonitorMode.Whisper);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.SwitchModeAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(["POST calls"], api.Commands);
        Assert.Empty(api.HungUp);
    }

    // The fresh leg answered: the one it replaces hears nothing the supervisor needs any more and goes quietly, so its
    // hang-up is not read as the supervisor walking away.
    [Fact]
    public async Task AReplacementLegAnswering_LetsTheLegItReplacesGo_Detached()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("monitor"));
        var replacement = SupervisorState("whisper");
        replacement.ReplacesCallControlId = Supervisor;
        api.WithLeg(ReplacementLeg, replacement);
        var sink = new Mock<ISupervisorLegEventSink>();
        var orchestrator = CreateOrchestrator(api, sink.Object);

        // Act
        await orchestrator.AdvanceAsync(Answered(ReplacementLeg, replacement), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([$"POST calls/{Supervisor}/actions/hangup"], api.Commands);
        Assert.True(FakeTelnyxCallControl.StateIn(api.BodyOf("POST", $"calls/{Supervisor}/actions/hangup")).Detached);
        sink.Verify(value => value.OnAnsweredAsync(TelnyxConstants.ProviderTechnicalName, Customer, ReplacementLeg, It.IsAny<CancellationToken>()), Times.Once);
    }

    // A fresh leg that ends without having answered (the phone refused it, or the engagement was stopped while it rang)
    // takes the leg it was to replace with it, keeping that leg's own state: whichever of the two the engagement names, its
    // end is reported, so the engagement never outlives both of its legs.
    [Fact]
    public async Task AReplacementLegEnding_EndsTheLegItWasToReplace_Too()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("monitor"));
        var replacement = SupervisorState("whisper");
        replacement.ReplacesCallControlId = Supervisor;
        var sink = new Mock<ISupervisorLegEventSink>();
        var orchestrator = CreateOrchestrator(api, sink.Object);

        // Act
        await orchestrator.AdvanceAsync(Hangup(ReplacementLeg, replacement), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([$"POST calls/{Supervisor}/actions/hangup"], api.Commands);
        Assert.False(api.BodyOf("POST", $"calls/{Supervisor}/actions/hangup").TryGetProperty("client_state", out _));
        sink.Verify(value => value.OnEndedAsync(TelnyxConstants.ProviderTechnicalName, Customer, ReplacementLeg, It.IsAny<DateTime?>(), It.IsAny<HangupCause?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Stop_HangsUpOnlyTheSupervisor_AndLeavesTheCallAsItIs()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("monitor"));
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(MonitorMode.Monitor);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.StopAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal([$"POST calls/{Supervisor}/actions/hangup"], api.Commands);

        // The supervisor's hang-up says it was the platform's, so it is not read as the supervisor walking away.
        var hangup = FakeTelnyxCallControl.StateIn(api.BodyOf("POST", $"calls/{Supervisor}/actions/hangup"));
        Assert.True(hangup.Detached);

        Assert.Equal([Supervisor], api.HungUp);
        Assert.Empty(api.Bridges);
    }

    // Live: Telnyx refused to bridge the customer to the supervising leg -- "Supervisor calls do not support commands" --
    // and the takeover failed. The supervisor's phone is rung with an ordinary leg carrying the engagement's token, the
    // customer is bridged to that once the phone answers, and only then are the supervising leg and the agent let go.
    [Fact]
    public async Task TakeOver_RingsTheSupervisorOnALegTheCustomerCanBeBridgedTo_ThenReleasesTheSupervisingLegAndTheAgent()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Customer, CustomerState());
        api.WithLeg(Supervisor, SupervisorState("whisper"));
        api.NextLegId = TakeOverLeg;
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(MonitorMode.Barge);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.TakeOverAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(TakeOverLeg, result.ProviderLegId);
        // No role switch first: live, switching the supervising leg to barge left the supervisor hearing silence, and the
        // customer is never alone anyway -- they are bridged to the new leg before the agent goes.
        Assert.Equal(
            [
                "POST calls",
                $"POST calls/{TakeOverLeg}/actions/bridge",
                $"POST calls/{Supervisor}/actions/hangup",
                $"GET calls/{Customer}",
                $"PUT calls/{Customer}/actions/client_state_update",
                $"GET calls/{Agent}",
                $"POST calls/{Agent}/actions/hangup",
            ],
            api.Commands);

        // An ordinary leg to the supervisor's phone, which answers it by the engagement's token.
        var dial = api.BodyOf("POST", "calls");
        Assert.Equal(SupervisorEndpoint, dial.GetProperty("to").GetString());
        Assert.False(dial.TryGetProperty("supervise_call_control_id", out _));
        Assert.Equal("cc-sv-take-token-1", dial.GetProperty("command_id").GetString());
        Assert.Equal("token-1", Assert.Single(dial.GetProperty("custom_headers").EnumerateArray()).GetProperty("value").GetString());

        var takeOverState = FakeTelnyxCallControl.StateIn(dial);
        Assert.True(takeOverState.TakesOver);
        Assert.Equal(TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent, takeOverState.Intent);
        Assert.Equal("token-1", takeOverState.MonitorToken);

        // The customer is on the new leg, which parks rather than hangs up if it is unbridged later, and names it as the
        // leg its own end hangs up.
        Assert.Equal((TakeOverLeg, Customer, "self"), Assert.Single(api.Bridges));
        Assert.True(TelnyxOutboundBridgeState.TryParse(api.LegStates[Customer], out var customer));
        Assert.Equal(TakeOverLeg, customer.PeerCallControlId);

        // The supervising leg and the agent go quietly: neither end is reported as the call ending.
        Assert.True(FakeTelnyxCallControl.StateIn(api.BodyOf("POST", $"calls/{Supervisor}/actions/hangup")).Detached);

        var released = FakeTelnyxCallControl.StateIn(api.BodyOf("POST", $"calls/{Agent}/actions/hangup"));
        Assert.True(released.Detached);
        Assert.Equal(TelnyxOutboundBridgeState.ContactCenterAgentLegIntent, released.Intent);
        Assert.Equal(Customer, released.PeerCallControlId);

        Assert.Equal(new[] { Agent, Supervisor }.Order(StringComparer.Ordinal), api.HungUp.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task TakeOver_WaitsForTheSupervisorsPhoneToAnswer_BeforeTheCustomerIsBridged()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("barge"));
        api.NextLegId = TakeOverLeg;
        api.AnswersAfterBridgeAttempts[TakeOverLeg] = 2;
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(MonitorMode.Barge);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.TakeOverAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(3, api.Commands.Count(command => command == $"POST calls/{TakeOverLeg}/actions/bridge"));
        Assert.Equal((TakeOverLeg, Customer, "self"), Assert.Single(api.Bridges));
        Assert.Contains(Agent, api.HungUp);
    }

    // A request that carries no token (a Contact Center call's) takes it from the supervising leg the phone answered.
    [Fact]
    public async Task TakeOver_WithoutTheToken_ReadsItFromTheSupervisingLeg()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("barge"));
        api.NextLegId = TakeOverLeg;
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(MonitorMode.Barge);
        request.SupervisorLegId = Supervisor;
        request.MonitorToken = null;

        // Act
        var result = await provider.TakeOverAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("token-1", FakeTelnyxCallControl.StateIn(api.BodyOf("POST", "calls")).MonitorToken);
    }

    [Fact]
    public async Task TakeOver_WhenTheCustomerCannotBeBridgedToTheSupervisor_IsRefused_AndOnlyTheNewLegIsLetGo()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("barge"));
        api.NextLegId = TakeOverLeg;
        api.RefuseBridgeFor.Add(TakeOverLeg);
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(MonitorMode.Barge);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.TakeOverAsync(request, TestContext.Current.CancellationToken);

        // Assert - refused at once, not waited on: only a leg still ringing is tried again.
        Assert.False(result.Succeeded);
        Assert.Equal(1, api.Commands.Count(command => command == $"POST calls/{TakeOverLeg}/actions/bridge"));
        Assert.Equal([TakeOverLeg], api.HungUp);
        Assert.True(FakeTelnyxCallControl.StateIn(api.BodyOf("POST", $"calls/{TakeOverLeg}/actions/hangup")).Detached);
    }

    // The answer of the leg a takeover rang is the takeover's to act on: it is not the engagement connecting again.
    [Fact]
    public async Task TheTakeOverLegAnswering_IsLeftToTheTakeover()
    {
        // Arrange
        var api = BridgedCall();
        var sink = new Mock<ISupervisorLegEventSink>();
        var state = SupervisorState("barge");
        state.TakesOver = true;
        api.WithLeg(TakeOverLeg, state);
        var orchestrator = CreateOrchestrator(api, sink.Object);

        // Act
        await orchestrator.AdvanceAsync(Answered(TakeOverLeg, state), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(api.Commands);
        sink.Verify(
            value => value.OnAnsweredAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TheReleasedAgentLegsHangup_DoesNotEndTheCall_ButAnAgentHangingUpStillDoes()
    {
        // Arrange
        var api = BridgedCall();
        var failures = new Mock<IContactCenterAgentLegFailureService>();
        var orchestrator = CreateOrchestrator(api, Mock.Of<ISupervisorLegEventSink>(), failures.Object);
        var agentState = AgentState();

        // Act
        await orchestrator.AdvanceAsync(Hangup(Agent, agentState.AsDetached()), TestContext.Current.CancellationToken);
        await orchestrator.AdvanceAsync(Hangup(Agent, agentState), TestContext.Current.CancellationToken);

        // Assert
        failures.Verify(
            value => value.RecordEndedAsync(TelnyxConstants.ProviderTechnicalName, Customer, Agent, It.IsAny<DateTime?>(), It.IsAny<HangupCause?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ASupervisorHangingUpTheirPhone_EndsTheirEngagement_AndLeavesTheCallAsItIs()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("monitor"));
        var sink = new Mock<ISupervisorLegEventSink>();
        var orchestrator = CreateOrchestrator(api, sink.Object);

        // Act
        var leg = await orchestrator.AdvanceAsync(Hangup(Supervisor, SupervisorState("monitor")), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelnyxOutboundBridgeLeg.DestinationLeg, leg);
        sink.Verify(value => value.OnEndedAsync(TelnyxConstants.ProviderTechnicalName, Customer, Supervisor, It.IsAny<DateTime?>(), It.IsAny<HangupCause?>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(api.Requests);
    }

    [Fact]
    // Live, a stop's own hang-up came back as a webhook that recorded the engagement's end a second time while the stop was
    // still recording it, and the stop failed with a ConcurrencyException (dashboard/stop 500). The platform's own release
    // is recorded by the platform.
    public async Task AReleasedSupervisorLegsHangup_IsNotReportedAgain_AndDoesNotMoveTheCall()
    {
        // Arrange
        var api = BridgedCall();
        var sink = new Mock<ISupervisorLegEventSink>();
        var orchestrator = CreateOrchestrator(api, sink.Object);
        var detached = SupervisorState("monitor");
        detached.Detached = true;

        // Act
        await orchestrator.AdvanceAsync(Hangup(Supervisor, detached), TestContext.Current.CancellationToken);

        // Assert
        sink.VerifyNoOtherCalls();
        Assert.Empty(api.Requests);
    }

    [Fact]
    public async Task ReleaseSupervisorLeg_HangsUpTheLegMarkedDetached()
    {
        // Arrange
        var api = BridgedCall();
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());

        // Act
        await provider.ReleaseSupervisorLegAsync(Supervisor, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([$"POST calls/{Supervisor}/actions/hangup"], api.Commands);
        Assert.True(FakeTelnyxCallControl.StateIn(api.BodyOf("POST", $"calls/{Supervisor}/actions/hangup")).Detached);
    }

    [Fact]
    public void TheProvider_AdvertisesListenWhisperAndBarge()
    {
        // Arrange
        var provider = TelnyxContactCenterProviderFactory.Create(new FakeTelnyxCallControl(), Resolver());

        // Act
        var capabilities = provider.Capabilities;

        // Assert
        Assert.True(capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.Monitor));
        Assert.True(capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.Whisper));
        Assert.True(capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.Barge));
        Assert.IsAssignableFrom<IContactCenterVoiceSupervisorInterventionProvider>(provider);
    }

    private static FakeTelnyxCallControl BridgedCall()
        => new FakeTelnyxCallControl()
            .WithLeg(Customer, null)
            .WithLeg(Agent, AgentState());

    private static TelnyxOutboundBridgeState AgentState()
        => new()
        {
            Intent = TelnyxOutboundBridgeState.ContactCenterAgentLegIntent,
            PeerCallControlId = Customer,
            RingUserId = "agent-user",
        };

    private static TelnyxOutboundBridgeState CustomerState()
        => new()
        {
            Intent = TelnyxOutboundBridgeState.ContactCenterAgentLegIntent,
            PeerCallControlId = Agent,
        };

    private static TelnyxOutboundBridgeState SupervisorState(string role)
        => new()
        {
            Intent = TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent,
            PeerCallControlId = Customer,
            PartyCallControlId = Agent,
            SupervisesInPlace = true,
            SupervisorRole = role,
            RingUserId = "supervisor-user",
            MonitorToken = "token-1",
        };

    private static ContactCenterVoiceMonitoringRequest Request(MonitorMode mode)
        => new()
        {
            InteractionId = "interaction-1",
            ProviderCallId = Customer,
            SupervisorId = "supervisor-user",
            Mode = mode,
            AgentLegId = Agent,
            MonitorToken = "token-1",
        };

    private static ITelnyxAgentEndpointResolver Resolver()
    {
        var resolver = new Mock<ITelnyxAgentEndpointResolver>();
        resolver
            .Setup(value => value.ResolveAsync("supervisor-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SupervisorEndpoint);

        return resolver.Object;
    }

    internal static TelnyxCallEvent Answered(string leg, TelnyxOutboundBridgeState state)
        => new()
        {
            EventType = "call.answered",
            CallControlId = leg,
            ClientState = state.ToClientStateJson(),
        };

    internal static TelnyxCallEvent Hangup(string leg, TelnyxOutboundBridgeState state)
        => new()
        {
            EventType = "call.hangup",
            CallControlId = leg,
            HangupCause = "normal_clearing",
            ClientState = state.ToClientStateJson(),
        };

    internal static TelnyxOutboundBridgeOrchestrator CreateOrchestrator(
        HttpMessageHandler handler,
        ISupervisorLegEventSink sink,
        IContactCenterAgentLegFailureService failureService = null,
        ISupervisorLegEventSink nextSink = null)
    {
        var options = new TelnyxOptions
        {
            IsEnabled = true,
            ApiKey = "KEY",
            ConnectionId = "connection-1",
            ApiBaseUrl = "https://api.telnyx.test/v2/",
            DefaultOutboundCallerId = "+15550000000",
        };

        var monitor = new Mock<IOptionsMonitor<TelnyxOptions>>();
        monitor.SetupGet(value => value.CurrentValue).Returns(options);

        var apiClient = new TelnyxApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.telnyx.test/v2/") },
            new OptionsWrapper<TelnyxOptions>(options),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);

        return new TelnyxOutboundBridgeOrchestrator(
            apiClient,
            NullLogger<TelnyxOutboundBridgeOrchestrator>.Instance,
            monitor.Object,
            failureService ?? new Mock<IContactCenterAgentLegFailureService>().Object,
            [],
            [],
            [],
            supervisorLegEventSinks: nextSink is null ? [sink] : [sink, nextSink]);
    }
}
