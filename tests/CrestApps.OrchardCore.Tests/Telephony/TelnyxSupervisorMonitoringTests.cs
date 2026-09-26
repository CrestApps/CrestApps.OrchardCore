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
/// is changed on that leg in place (<c>switch_supervisor_role</c>), and stopping only hangs it up. Live, moving the call
/// into a conference for a supervisor left the customer and the agent unable to hear each other -- or the supervisor --
/// in every mode, so nobody is moved.
/// </summary>
public sealed class TelnyxSupervisorMonitoringTests
{
    private const string Customer = "customer-leg";
    private const string Agent = "agent-leg";
    private const string Supervisor = "supervisor-leg";
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

        // Telnyx attaches the answered leg to the agent's: a whisper is heard by the agent alone.
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

    // The answered leg is already on the call. Its role is set again once, in case the mode changed while it rang.
    [Theory]
    [InlineData("monitor")]
    [InlineData("whisper")]
    [InlineData("barge")]
    public async Task SupervisorAnswers_ABridgedCall_NobodyIsMoved_AndTheLegTakesTheModeItWasLastGiven(string role)
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState(role));
        var sink = new Mock<ISupervisorLegEventSink>();
        var orchestrator = CreateOrchestrator(api, sink.Object);

        // Act
        await orchestrator.AdvanceAsync(Answered(Supervisor, SupervisorState(role)), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([$"POST calls/{Supervisor}/actions/switch_supervisor_role"], api.Commands);
        Assert.Equal(role, api.BodyOf("POST", $"calls/{Supervisor}/actions/switch_supervisor_role").GetProperty("role").GetString());
        Assert.Empty(api.Conferences);
        Assert.Empty(api.Bridges);
        Assert.Empty(api.HungUp);

        sink.Verify(value => value.OnAnsweredAsync(TelnyxConstants.ProviderTechnicalName, Customer, Supervisor, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(MonitorMode.Monitor, "monitor")]
    [InlineData(MonitorMode.Whisper, "whisper")]
    [InlineData(MonitorMode.Barge, "barge")]
    public async Task SwitchMode_ChangesTheSupervisorsRoleOnTheirOwnLeg_WithoutRingingThemAgain_OrMovingAnybody(MonitorMode mode, string role)
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("monitor"));
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(mode);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.SwitchModeAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal([$"POST calls/{Supervisor}/actions/switch_supervisor_role"], api.Commands);
        Assert.Equal(role, api.BodyOf("POST", $"calls/{Supervisor}/actions/switch_supervisor_role").GetProperty("role").GetString());
        Assert.Empty(api.Conferences);
        Assert.Empty(api.HungUp);
    }

    [Fact]
    public async Task SwitchMode_BeforeThePhoneAnswered_ChangesTheRoleTheLegTakesWhenItAnswers()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("monitor"));
        api.Unanswered.Add(Supervisor);
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(MonitorMode.Barge);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.SwitchModeAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(
            [$"POST calls/{Supervisor}/actions/switch_supervisor_role", $"GET calls/{Supervisor}", $"PUT calls/{Supervisor}/actions/client_state_update"],
            api.Commands);
        Assert.True(TelnyxOutboundBridgeState.TryParse(api.LegStates[Supervisor], out var state));
        Assert.Equal("barge", state.SupervisorRole);
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

    [Fact]
    public async Task TakeOver_MakesTheSupervisorHeardByBoth_BridgesThemToTheCustomer_ThenReleasesOnlyTheAgent_MarkedDetached()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("whisper"));
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(MonitorMode.Barge);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.TakeOverAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(
            [
                $"POST calls/{Supervisor}/actions/switch_supervisor_role",
                $"POST calls/{Supervisor}/actions/bridge",
                $"GET calls/{Agent}",
                $"POST calls/{Agent}/actions/hangup",
            ],
            api.Commands);

        Assert.Equal("barge", api.BodyOf("POST", $"calls/{Supervisor}/actions/switch_supervisor_role").GetProperty("role").GetString());

        // The customer is now on the supervisor's own leg, which parks rather than hangs up if it is unbridged later.
        Assert.Equal((Supervisor, Customer, "self"), Assert.Single(api.Bridges));

        var released = FakeTelnyxCallControl.StateIn(api.BodyOf("POST", $"calls/{Agent}/actions/hangup"));
        Assert.True(released.Detached);
        Assert.Equal(TelnyxOutboundBridgeState.ContactCenterAgentLegIntent, released.Intent);
        Assert.Equal(Customer, released.PeerCallControlId);

        Assert.Equal([Agent], api.HungUp);
    }

    [Fact]
    public async Task TakeOver_WhenTheCustomerCannotBeBridgedToTheSupervisor_IsRefused_AndTheAgentStays()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("barge"));
        api.RefuseBridgeFor.Add(Supervisor);
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(MonitorMode.Barge);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.TakeOverAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(api.HungUp);
    }

    [Fact]
    public async Task TakeOver_BeforeTheSupervisorIsOnTheCall_IsRefused_AndTheAgentStays()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("monitor"));
        api.Unanswered.Add(Supervisor);
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(MonitorMode.Barge);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.TakeOverAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(api.HungUp);
        Assert.Empty(api.Bridges);
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
