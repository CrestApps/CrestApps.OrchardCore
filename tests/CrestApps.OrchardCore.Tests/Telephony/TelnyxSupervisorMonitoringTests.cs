using System.Text.Json;
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
/// the customer's with <c>park_after_unbridge=self</c>; the supervisor is rung on their own soft phone, and only once
/// they answer is the call moved into a conference (created from the customer's leg, which parks the agent's leg, which
/// then joins) where the supervisor joins with a Telnyx supervisor role. Nobody is hung up to get there, nobody hears
/// a tone, and the call goes back on its bridge when the last supervisor leaves.
/// </summary>
public sealed class TelnyxSupervisorMonitoringTests
{
    private const string Customer = "customer-leg";
    private const string Agent = "agent-leg";
    private const string Supervisor = "supervisor-leg";
    private const string SupervisorEndpoint = "sip:gencredSupervisor@sip.telnyx.com";
    private const string Conference = "cc-sv-customer-leg";

    [Theory]
    [InlineData(MonitorMode.Monitor, "monitor")]
    [InlineData(MonitorMode.Whisper, "whisper")]
    [InlineData(MonitorMode.Barge, "barge")]
    public async Task Engage_RingsTheSupervisorsRegisteredPhone_WithALegThatNamesTheCallTheRoleAndTheToken(MonitorMode mode, string role)
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

        // Nothing moves until the supervisor answers: the one command is the ring to their phone.
        Assert.Equal(["POST calls"], api.Commands);

        var dial = api.BodyOf("POST", "calls");
        Assert.Equal(SupervisorEndpoint, dial.GetProperty("to").GetString());
        Assert.Equal("connection-1", dial.GetProperty("connection_id").GetString());
        Assert.False(dial.TryGetProperty("outbound_voice_profile_id", out _));
        Assert.Equal("cc-sv-leg-token-1", dial.GetProperty("command_id").GetString());

        var header = Assert.Single(dial.GetProperty("custom_headers").EnumerateArray());
        Assert.Equal(TelnyxConstants.MonitorLegSipHeader, header.GetProperty("name").GetString());
        Assert.Equal("token-1", header.GetProperty("value").GetString());

        var state = FakeTelnyxCallControl.StateIn(dial);
        Assert.Equal(TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent, state.Intent);
        Assert.Equal(Customer, state.PeerCallControlId);
        Assert.Equal(Agent, state.PartyCallControlId);
        Assert.Equal(Conference, state.ConferenceName);
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

    // Every mode joins as a whisperer naming who hears the supervisor: the agent while listening (the supervisor's phone
    // keeps its microphone off) or coaching, everybody on the call when joining it. Live, a supervisor joined muted -- or in
    // Telnyx's "monitor" role -- left the customer and the agent unable to hear each other, and nothing changed of the
    // supervisor afterwards brought it back; nobody is ever muted in the conference now.
    [Theory]
    [InlineData("monitor", false)]
    [InlineData("whisper", false)]
    [InlineData("barge", true)]
    public async Task SupervisorAnswers_ABridgedCall_IsMovedIntoASilentConference_WithBothPartiesKept_AndTheSupervisorJoinsHeardByTheirMode(string role, bool heardByEverybody)
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState(role));
        var sink = new Mock<ISupervisorLegEventSink>();
        var orchestrator = CreateOrchestrator(api, sink.Object);

        // Act
        await orchestrator.AdvanceAsync(Answered(Supervisor, SupervisorState(role)), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                "GET conferences",
                "POST conferences",
                "POST conferences/conf-1/actions/join",
                .. heardByEverybody ? new[] { "GET conferences/conf-1/participants" } : [],
                "POST conferences/conf-1/actions/join",
                "GET conferences/conf-1/participants",
            ],
            api.Commands);

        // The conference is created from the customer's leg -- which parks the agent's -- with no tone.
        var create = api.BodyOf("POST", "conferences");
        Assert.Equal(Conference, create.GetProperty("name").GetString());
        Assert.Equal(Customer, create.GetProperty("call_control_id").GetString());
        Assert.Equal("never", create.GetProperty("beep_enabled").GetString());

        var joins = api.Requests.Where(request => request.Path == "conferences/conf-1/actions/join").Select(request => request.Body).ToArray();

        // The agent joins as an ordinary participant, silently, and not as the one whose leaving ends the conference.
        Assert.Equal(Agent, joins[0].GetProperty("call_control_id").GetString());
        Assert.Equal("never", joins[0].GetProperty("beep_enabled").GetString());
        Assert.False(joins[0].TryGetProperty("supervisor_role", out _));
        Assert.False(joins[0].TryGetProperty("end_conference_on_exit", out _));

        Assert.Equal(Supervisor, joins[1].GetProperty("call_control_id").GetString());
        Assert.Equal("whisper", joins[1].GetProperty("supervisor_role").GetString());
        Assert.Equal("never", joins[1].GetProperty("beep_enabled").GetString());

        string[] hearers = heardByEverybody ? [Customer, Agent] : [Agent];
        Assert.Equal(hearers, joins[1].GetProperty("whisper_call_control_ids").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(hearers, api.Conferences["conf-1"].Whispers[Supervisor]);

        // Nobody is muted, and no other supervisor role is ever asked for.
        Assert.Empty(api.Conferences["conf-1"].Muted);
        Assert.DoesNotContain(api.Requests, request => request.Body.ValueKind == JsonValueKind.Object && request.Body.TryGetProperty("mute", out _));
        Assert.DoesNotContain(api.Commands, command => command.EndsWith("/actions/mute", StringComparison.Ordinal));

        // Both parties are still on the call, together, and nobody was hung up.
        Assert.Equal([Customer, Agent, Supervisor], api.Conferences["conf-1"].Participants);
        Assert.Empty(api.HungUp);

        sink.Verify(value => value.OnAnsweredAsync(TelnyxConstants.ProviderTechnicalName, Customer, Supervisor, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ASecondSupervisorAnswers_JoinsTheConferenceTheCallIsAlreadyIn_WithoutMovingAnybody()
    {
        // Arrange
        var api = BridgedCall();
        api.WithConference(Conference, Customer, Agent, "first-supervisor-leg");
        var orchestrator = CreateOrchestrator(api, Mock.Of<ISupervisorLegEventSink>());

        // Act
        await orchestrator.AdvanceAsync(Answered(Supervisor, SupervisorState("barge")), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ["GET conferences", "GET conferences/conf-1/participants", "POST conferences/conf-1/actions/join", "GET conferences/conf-1/participants"],
            api.Commands);
        var join = api.BodyOf("POST", "conferences/conf-1/actions/join");
        Assert.Equal(Supervisor, join.GetProperty("call_control_id").GetString());

        // Joining the call is being heard by everybody on it -- the other supervisor too.
        Assert.Equal([Customer, Agent, "first-supervisor-leg"], join.GetProperty("whisper_call_control_ids").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task WhenTheAgentCannotJoin_TheCallGoesBackOnItsBridge_AndTheSupervisorIsHungUp()
    {
        // Arrange
        var api = BridgedCall();
        api.RefuseJoinFor.Add(Agent);
        var orchestrator = CreateOrchestrator(api, Mock.Of<ISupervisorLegEventSink>());

        // Act
        await orchestrator.AdvanceAsync(Answered(Supervisor, SupervisorState("monitor")), TestContext.Current.CancellationToken);

        // Assert
        var bridge = Assert.Single(api.Bridges);
        Assert.Equal((Agent, Customer, "self"), bridge);
        Assert.Equal([Supervisor], api.HungUp);
        Assert.DoesNotContain(Customer, api.HungUp);
        Assert.DoesNotContain(Agent, api.HungUp);
    }

    [Theory]
    [InlineData(MonitorMode.Whisper, false)]
    [InlineData(MonitorMode.Monitor, false)]
    [InlineData(MonitorMode.Barge, true)]
    public async Task SwitchMode_ChangesWhoHearsTheSupervisorInPlace_WithoutRingingThemAgain_OrMutingAnybody(MonitorMode mode, bool heardByEverybody)
    {
        // Arrange
        var api = BridgedCall();
        api.WithConference(Conference, Customer, Agent, Supervisor).Whispers[Supervisor] = [Agent];
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(mode);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.SwitchModeAsync(request, TestContext.Current.CancellationToken);

        // Assert - one update, then read back to confirm Telnyx applied it.
        Assert.True(result.Succeeded);
        Assert.Equal(
            ["GET conferences", "GET conferences/conf-1/participants", "POST conferences/conf-1/actions/update", "GET conferences/conf-1/participants"],
            api.Commands);

        var update = api.BodyOf("POST", "conferences/conf-1/actions/update");
        string[] hearers = heardByEverybody ? [Customer, Agent] : [Agent];
        Assert.Equal(Supervisor, update.GetProperty("call_control_id").GetString());
        Assert.Equal("whisper", update.GetProperty("supervisor_role").GetString());
        Assert.Equal(hearers, update.GetProperty("whisper_call_control_ids").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(hearers, api.Conferences["conf-1"].Whispers[Supervisor]);
        Assert.Empty(api.Conferences["conf-1"].Muted);
        Assert.Empty(api.HungUp);
    }

    // Live, changing a coaching supervisor to joining the call answered 200 and changed nothing: nobody heard them.
    [Fact]
    public async Task SwitchMode_WhenTelnyxDoesNotApplyTheChange_TheSupervisorLeavesAndRejoinsHeardByTheirNewMode()
    {
        // Arrange
        var api = BridgedCall();
        api.IgnoreParticipantUpdates = true;
        api.WithConference(Conference, Customer, Agent, Supervisor).Whispers[Supervisor] = [Agent];
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(MonitorMode.Barge);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.SwitchModeAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(
            [
                "GET conferences",
                "GET conferences/conf-1/participants",
                "POST conferences/conf-1/actions/update",
                "GET conferences/conf-1/participants",
                "POST conferences/conf-1/actions/leave",
                "POST conferences/conf-1/actions/join",
                "GET conferences/conf-1/participants",
            ],
            api.Commands);

        var rejoin = api.BodyOf("POST", "conferences/conf-1/actions/join");
        Assert.Equal(Supervisor, rejoin.GetProperty("call_control_id").GetString());
        Assert.Equal("whisper", rejoin.GetProperty("supervisor_role").GetString());
        Assert.Equal([Customer, Agent], api.Conferences["conf-1"].Whispers[Supervisor]);
        Assert.Equal([Customer, Agent, Supervisor], api.Conferences["conf-1"].Participants);
        Assert.Empty(api.HungUp);
    }

    [Fact]
    public async Task SwitchMode_BeforeThePhoneAnswered_ChangesTheRoleTheLegWillJoinWith()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("monitor"));
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(MonitorMode.Barge);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.SwitchModeAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(["GET conferences", $"GET calls/{Supervisor}", $"PUT calls/{Supervisor}/actions/client_state_update"], api.Commands);
        Assert.True(TelnyxOutboundBridgeState.TryParse(api.LegStates[Supervisor], out var state));
        Assert.Equal("barge", state.SupervisorRole);
    }

    [Fact]
    public async Task Stop_HangsUpOnlyTheSupervisor_AndPutsTheCallBackOnItsBridge()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("monitor"));
        api.WithConference(Conference, Customer, Agent, Supervisor);
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(MonitorMode.Monitor);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.StopAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(
            [
                $"POST calls/{Supervisor}/actions/hangup",
                "GET conferences",
                "GET conferences/conf-1/participants",
                $"GET calls/{Agent}",
                "POST conferences/conf-1/actions/leave",
                "POST conferences/conf-1/actions/leave",
                $"POST calls/{Agent}/actions/bridge",
            ],
            api.Commands);

        // The supervisor's hang-up says it was the platform's, so it is not read as the supervisor walking away.
        var hangup = FakeTelnyxCallControl.StateIn(api.BodyOf("POST", $"calls/{Supervisor}/actions/hangup"));
        Assert.True(hangup.Detached);

        Assert.Equal([Supervisor], api.HungUp);
        Assert.Equal((Agent, Customer, "self"), Assert.Single(api.Bridges));
    }

    [Fact]
    public async Task Stop_WhileAnotherSupervisorStillListens_LeavesTheCallInTheConference()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("monitor"));
        api.WithLeg("other-supervisor-leg", SupervisorState("whisper"));
        api.WithConference(Conference, Customer, Agent, Supervisor, "other-supervisor-leg");
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(MonitorMode.Monitor);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.StopAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Empty(api.Bridges);
        Assert.DoesNotContain(api.Commands, command => command.EndsWith("/actions/leave", StringComparison.Ordinal));
        Assert.Equal([Customer, Agent, "other-supervisor-leg"], api.Conferences["conf-1"].Participants);
    }

    [Fact]
    public async Task TakeOver_MakesTheSupervisorHeardByTheCustomer_ThenReleasesOnlyTheAgent_MarkedDetached()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("monitor"));
        api.WithConference(Conference, Customer, Agent, Supervisor);
        var provider = TelnyxContactCenterProviderFactory.Create(api, Resolver());
        var request = Request(MonitorMode.Barge);
        request.SupervisorLegId = Supervisor;

        // Act
        var result = await provider.TakeOverAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(
            [
                "GET conferences",
                "GET conferences/conf-1/participants",
                "POST conferences/conf-1/actions/update",
                "GET conferences/conf-1/participants",
                $"GET calls/{Agent}",
                $"POST calls/{Agent}/actions/hangup",
            ],
            api.Commands);

        var update = api.BodyOf("POST", "conferences/conf-1/actions/update");
        Assert.Equal("whisper", update.GetProperty("supervisor_role").GetString());
        Assert.Equal([Customer, Agent], update.GetProperty("whisper_call_control_ids").EnumerateArray().Select(item => item.GetString()));

        var released = FakeTelnyxCallControl.StateIn(api.BodyOf("POST", $"calls/{Agent}/actions/hangup"));
        Assert.True(released.Detached);
        Assert.Equal(TelnyxOutboundBridgeState.ContactCenterAgentLegIntent, released.Intent);
        Assert.Equal(Customer, released.PeerCallControlId);

        Assert.Equal([Agent], api.HungUp);
        Assert.Equal([Customer, Supervisor], api.Conferences["conf-1"].Participants);
    }

    [Fact]
    public async Task TakeOver_BeforeTheSupervisorIsOnTheCall_IsRefused_AndTheAgentStays()
    {
        // Arrange
        var api = BridgedCall();
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
    public async Task ASupervisorHangingUpTheirPhone_EndsTheirEngagement_AndPutsTheCallBackOnItsBridge()
    {
        // Arrange
        var api = BridgedCall();
        api.WithLeg(Supervisor, SupervisorState("monitor"));
        api.WithConference(Conference, Customer, Agent);
        var sink = new Mock<ISupervisorLegEventSink>();
        var orchestrator = CreateOrchestrator(api, sink.Object);

        // Act
        var leg = await orchestrator.AdvanceAsync(Hangup(Supervisor, SupervisorState("monitor")), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelnyxOutboundBridgeLeg.DestinationLeg, leg);
        sink.Verify(value => value.OnEndedAsync(TelnyxConstants.ProviderTechnicalName, Customer, Supervisor, It.IsAny<DateTime?>(), It.IsAny<HangupCause?>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal((Agent, Customer, "self"), Assert.Single(api.Bridges));
    }

    [Fact]
    // Live, a stop's own hang-up came back as a webhook that recorded the engagement's end a second time while the stop was
    // still recording it, and the stop failed with a ConcurrencyException (dashboard/stop 500). The platform's own release
    // is recorded by the platform.
    public async Task AReleasedSupervisorLegsHangup_IsNotReportedAgain_AndDoesNotMoveTheCall()
    {
        // Arrange
        var api = BridgedCall();
        api.WithConference(Conference, Customer, Agent);
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
            ConferenceName = Conference,
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
