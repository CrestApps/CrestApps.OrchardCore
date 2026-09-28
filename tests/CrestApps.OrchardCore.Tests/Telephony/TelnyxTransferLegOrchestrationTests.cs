using System.Net;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The webhook side of transferring a number dialed from the soft phone: the transfer leg answering or not, a consult's
/// destination answering or leaving, and each of the agent's legs hanging up while a transfer is under way.
/// </summary>
public sealed class TelnyxTransferLegOrchestrationTests
{
    private const string AgentLeg = "agent-leg-1";
    private const string RemoteLeg = "remote-leg-1";
    private const string TransferLeg = "xfer-leg-1";
    private const string ConsultLeg = "consult-leg-1";
    private const string NumberLeg = "number-leg-1";
    private const string Ok = """{"data":{"result":"ok"}}""";

    [Fact]
    public async Task ColleagueAnswersABlindTransfer_TheCallerIsBridgedToThem_AndTheAgentReleased()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var orchestrator = CreateOrchestrator(handler, []);

        // Act
        var leg = await orchestrator.AdvanceAsync(Event("call.answered", TransferLeg, TelnyxBridgedTransferTests.TransferLegState()), TestContext.Current.CancellationToken);

        // Assert - the colleague's answer reaches their history, so their phone sees the call connect.
        Assert.Equal(TelnyxOutboundBridgeLeg.None, leg);
        Assert.Equal(
            [
                $"POST /v2/calls/{TransferLeg}/actions/bridge",
                $"PUT /v2/calls/{TransferLeg}/actions/client_state_update",
                $"PUT /v2/calls/{RemoteLeg}/actions/client_state_update",
                $"POST /v2/calls/{AgentLeg}/actions/hangup",
            ],
            handler.Requests.Select(Describe));

        var bridge = handler.Requests[0];
        Assert.Equal(RemoteLeg, ReadString(bridge.Body, "call_control_id"));
        Assert.Equal("self", ReadString(bridge.Body, "park_after_unbridge"));

        // The colleague's leg now names the caller as a keypad dial does, so it can be transferred or merged again.
        var colleague = State(handler.Requests[1].Body);
        Assert.True(colleague.IsBridgedDialAgentLeg);
        Assert.Equal(RemoteLeg, colleague.PeerCallControlId);
        Assert.Null(colleague.Destination);
        Assert.Equal(TransferLeg, State(handler.Requests[2].Body).PeerCallControlId);
        Assert.True(State(handler.Requests[3].Body).Detached);
    }

    [Fact]
    public async Task NumberAnswersABlindTransfer_TheTwoOutsidePartiesAreJoined_AndReleaseEachOther()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var orchestrator = CreateOrchestrator(handler, []);
        var state = TelnyxBridgedTransferTests.TransferLegState();
        state.TargetUserId = null;

        // Act
        var leg = await orchestrator.AdvanceAsync(Event("call.answered", TransferLeg, state), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelnyxOutboundBridgeLeg.DestinationLeg, leg);
        Assert.False(HasProperty(handler.Requests[0].Body, "park_after_unbridge"));

        var target = State(handler.Requests[1].Body);
        Assert.Equal(RemoteLeg, target.ReleaseWithCallControlId);
        Assert.Equal(AgentLeg, target.PeerCallControlId);
        Assert.True(target.Detached);

        var party = State(handler.Requests[2].Body);
        Assert.Equal(TransferLeg, party.ReleaseWithCallControlId);
        Assert.True(party.Detached);
    }

    [Theory]
    [InlineData("call.initiated")]
    [InlineData("call.bridged")]
    public async Task ATransferLegRinging_IsNotShownOnThePhone(string eventType)
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        var orchestrator = CreateOrchestrator(handler, []);

        // Act
        var leg = await orchestrator.AdvanceAsync(Event(eventType, TransferLeg, TelnyxBridgedTransferTests.TransferLegState()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelnyxOutboundBridgeLeg.DestinationLeg, leg);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("NO_ANSWER")]
    [InlineData("CALL_REJECTED")]
    [InlineData("TIMEOUT")]
    public async Task ABlindTransferNobodyAnswers_LeavesTheCallerWithTheAgent(string cause)
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, Status(true, TelnyxBridgedDialTests.AgentLeg(peer: RemoteLeg).WithPendingTransfer(TransferLeg)))
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var orchestrator = CreateOrchestrator(handler, []);
        var hangup = Event("call.hangup", TransferLeg, TelnyxBridgedTransferTests.TransferLegState());
        hangup.HangupCause = cause;

        // Act
        await orchestrator.AdvanceAsync(hangup, TestContext.Current.CancellationToken);

        // Assert - nothing is hung up; both legs stop waiting for the transfer.
        Assert.Equal(
            [
                $"GET /v2/calls/{AgentLeg}",
                $"PUT /v2/calls/{AgentLeg}/actions/client_state_update",
                $"PUT /v2/calls/{RemoteLeg}/actions/client_state_update",
            ],
            handler.Requests.Select(Describe));
        Assert.Null(State(handler.Requests[1].Body).PendingTransferCallControlId);
        Assert.Equal(RemoteLeg, State(handler.Requests[1].Body).PeerCallControlId);
        Assert.Null(State(handler.Requests[2].Body).PendingTransferCallControlId);
    }

    [Fact]
    public async Task AColleagueDoesNotAnswer_AfterTheAgentLeft_TheCallerIsSentToTheColleaguesVoicemail()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.NotFound, """{"errors":[{"code":"90015"}]}""")
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var orchestrator = CreateOrchestrator(handler, []);
        var hangup = Event("call.hangup", TransferLeg, TelnyxBridgedTransferTests.TransferLegState());
        hangup.HangupCause = "NO_ANSWER";

        // Act
        await orchestrator.AdvanceAsync(hangup, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([$"GET /v2/calls/{AgentLeg}", $"POST /v2/calls/{RemoteLeg}/actions/record_start"], handler.Requests.Select(Describe));
        Assert.Contains("user-2", Encoding.UTF8.GetString(Convert.FromBase64String(ReadString(handler.Requests[1].Body, "client_state"))), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANumberDoesNotAnswer_AfterTheAgentLeft_TheCallerIsReleased()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, Status(false, TelnyxBridgedDialTests.AgentLeg(peer: RemoteLeg)))
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var orchestrator = CreateOrchestrator(handler, []);
        var state = TelnyxBridgedTransferTests.TransferLegState();
        state.TargetUserId = null;
        var hangup = Event("call.hangup", TransferLeg, state);
        hangup.HangupCause = "NO_ANSWER";

        // Act
        await orchestrator.AdvanceAsync(hangup, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([$"GET /v2/calls/{AgentLeg}", $"POST /v2/calls/{RemoteLeg}/actions/hangup"], handler.Requests.Select(Describe));
    }

    [Fact]
    public async Task TheAgentHangsUpWhileATransferRings_TheCallerIsKeptForIt()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        var orchestrator = CreateOrchestrator(handler, []);

        // Act
        await orchestrator.AdvanceAsync(
            Event("call.hangup", AgentLeg, TelnyxBridgedDialTests.AgentLeg(peer: RemoteLeg).WithPendingTransfer(TransferLeg)),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TheCallerHangsUpWhileATransferRings_TheRingingLegGoesToo()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var orchestrator = CreateOrchestrator(handler, []);
        var hangup = Event("call.hangup", RemoteLeg, new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
            PeerCallControlId = AgentLeg,
            PendingTransferCallControlId = TransferLeg,
        });
        hangup.HangupCause = "NORMAL_CLEARING";

        // Act
        await orchestrator.AdvanceAsync(hangup, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([$"POST /v2/calls/{AgentLeg}/actions/hangup", $"POST /v2/calls/{TransferLeg}/actions/hangup"], handler.Requests.Select(Describe));
    }

    [Fact]
    public async Task AnOutsidePartyTheCallWasHandedToHangsUp_ReleasesTheOther()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var orchestrator = CreateOrchestrator(handler, []);
        var hangup = Event("call.hangup", RemoteLeg, new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
            PeerCallControlId = AgentLeg,
            ReleaseWithCallControlId = NumberLeg,
            Detached = true,
        });
        hangup.HangupCause = "NORMAL_CLEARING";

        // Act
        await orchestrator.AdvanceAsync(hangup, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([$"POST /v2/calls/{NumberLeg}/actions/hangup"], handler.Requests.Select(Describe));
    }

    [Fact]
    public async Task AConsultWithAColleague_RingsThemOnATransferLeg_RecordedInTheirHistory()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, $$$"""{"data":{"call_control_id":"{{{TransferLeg}}}"}}""")
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var history = new List<TelephonyInteraction>();
        var orchestrator = CreateOrchestrator(handler, history);
        var consult = TelnyxBridgedTransferTests.ConsultState(answered: false);
        consult.PeerCallControlId = null;
        consult.Destination = "sip:gencred2@sip.telnyx.com";
        consult.TargetUserId = "user-2";

        // Act
        await orchestrator.AdvanceAsync(Event("call.answered", ConsultLeg, consult), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["POST /v2/calls", $"PUT /v2/calls/{ConsultLeg}/actions/client_state_update"], handler.Requests.Select(Describe));

        var ring = handler.Requests[0];
        Assert.Equal("sip:gencred2@sip.telnyx.com", ReadString(ring.Body, "to"));
        Assert.False(HasProperty(ring.Body, "outbound_voice_profile_id"));

        var leg = State(ring.Body);
        Assert.Equal(TelnyxOutboundBridgeState.TransferLegIntent, leg.Intent);
        Assert.Equal(ConsultLeg, leg.PeerCallControlId);
        Assert.Equal(AgentLeg, leg.TransferOfCallControlId);
        Assert.Equal($"consult-{ConsultLeg}", leg.ConferenceName);

        Assert.Equal(TransferLeg, State(handler.Requests[1].Body).PeerCallControlId);
        Assert.Equal(TransferLeg, Assert.Single(history).CallId);
    }

    [Fact]
    public async Task AConsultWithANumber_DialsIt_NamingTheCallBeingConsultedAbout()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, $$$"""{"data":{"call_control_id":"{{{NumberLeg}}}"}}""")
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var orchestrator = CreateOrchestrator(handler, []);
        var consult = TelnyxBridgedTransferTests.ConsultState(answered: false);
        consult.PeerCallControlId = null;

        // Act
        await orchestrator.AdvanceAsync(Event("call.answered", ConsultLeg, consult), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["POST /v2/calls", $"PUT /v2/calls/{ConsultLeg}/actions/client_state_update"], handler.Requests.Select(Describe));
        Assert.Equal("voice-profile-1", ReadString(handler.Requests[0].Body, "outbound_voice_profile_id"));
        Assert.Equal(AgentLeg, State(handler.Requests[0].Body).TransferOfCallControlId);
    }

    [Fact]
    public async Task TheConsultedColleagueAnswers_JoinsTheAgentsConsultLeg_AndTheConsultIsMarkedAnswered()
    {
        // Arrange
        var consult = TelnyxBridgedTransferTests.ConsultState(answered: false);
        consult.PeerCallControlId = TransferLeg;
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, """{"data":{"id":"conference-9"}}""")
            .RespondWith(HttpStatusCode.OK, Ok)
            .RespondWith(HttpStatusCode.OK, Status(true, consult))
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var orchestrator = CreateOrchestrator(handler, []);

        // Act
        var leg = await orchestrator.AdvanceAsync(Event("call.answered", TransferLeg, ConsultTransferLegState()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelnyxOutboundBridgeLeg.None, leg);
        Assert.Equal(
            [
                "POST /v2/conferences",
                "POST /v2/conferences/conference-9/actions/join",
                $"GET /v2/calls/{ConsultLeg}",
                $"PUT /v2/calls/{ConsultLeg}/actions/client_state_update",
            ],
            handler.Requests.Select(Describe));
        Assert.Equal(TransferLeg, ReadString(handler.Requests[0].Body, "call_control_id"));
        Assert.Equal($"consult-{ConsultLeg}", ReadString(handler.Requests[0].Body, "name"));

        // Neither of them leaving ends the consult for the other, so the agent hanging up can still hand the call over.
        Assert.Equal(ConsultLeg, ReadString(handler.Requests[1].Body, "call_control_id"));
        Assert.False(HasProperty(handler.Requests[1].Body, "end_conference_on_exit"));
        Assert.True(State(handler.Requests[3].Body).TargetAnswered);
    }

    [Fact]
    public async Task TheConsultedNumberAnswers_IsBridged_AndTheConsultIsMarkedAnswered()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, Ok)
            .RespondWith(HttpStatusCode.OK, Status(true, TelnyxBridgedTransferTests.ConsultState(answered: false)))
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var orchestrator = CreateOrchestrator(handler, []);

        // Act
        await orchestrator.AdvanceAsync(Event("call.answered", NumberLeg, ConsultNumberState()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                $"POST /v2/calls/{ConsultLeg}/actions/bridge",
                $"GET /v2/calls/{ConsultLeg}",
                $"PUT /v2/calls/{ConsultLeg}/actions/client_state_update",
            ],
            handler.Requests.Select(Describe));
        Assert.True(State(handler.Requests[2].Body).TargetAnswered);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheConsultedDestinationHangsUp_TheConsultEnds_AndTheCallIsTheAgentsAgain(bool colleague)
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, Status(true, TelnyxBridgedDialTests.AgentLeg(peer: RemoteLeg).WithPendingTransfer(ConsultLeg)))
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var orchestrator = CreateOrchestrator(handler, []);
        var hangup = colleague
            ? Event("call.hangup", TransferLeg, ConsultTransferLegState())
            : Event("call.hangup", NumberLeg, ConsultNumberState());
        hangup.HangupCause = "NORMAL_CLEARING";

        // Act
        await orchestrator.AdvanceAsync(hangup, TestContext.Current.CancellationToken);

        // Assert - the caller is not touched: they are still held on the agent's leg.
        Assert.Equal(
            [
                $"GET /v2/calls/{AgentLeg}",
                $"PUT /v2/calls/{AgentLeg}/actions/client_state_update",
                $"POST /v2/calls/{ConsultLeg}/actions/hangup",
            ],
            handler.Requests.Select(Describe));
        Assert.Null(State(handler.Requests[1].Body).PendingTransferCallControlId);
        Assert.True(State(handler.Requests[2].Body).Detached);
    }

    // Like most phone systems: hanging up while talking to the destination hands them the call.
    [Fact]
    public async Task TheAgentHangsUpTheConsultAfterTheDestinationAnswered_TheTransferCompletes()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var orchestrator = CreateOrchestrator(handler, []);

        // Act
        await orchestrator.AdvanceAsync(Event("call.hangup", ConsultLeg, TelnyxBridgedTransferTests.ConsultState(answered: true)), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                $"POST /v2/calls/{NumberLeg}/actions/bridge",
                $"PUT /v2/calls/{NumberLeg}/actions/client_state_update",
                $"PUT /v2/calls/{RemoteLeg}/actions/client_state_update",
                $"POST /v2/calls/{AgentLeg}/actions/hangup",
            ],
            handler.Requests.Select(Describe));
        Assert.Equal(RemoteLeg, ReadString(handler.Requests[0].Body, "call_control_id"));
    }

    [Fact]
    public async Task TheConsultEndsBeforeTheDestinationAnswered_ReleasesTheDestination_AndGivesTheCallBack()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, Ok)
            .RespondWith(HttpStatusCode.OK, Status(true, TelnyxBridgedDialTests.AgentLeg(peer: RemoteLeg).WithPendingTransfer(ConsultLeg)))
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var orchestrator = CreateOrchestrator(handler, []);

        // Act
        await orchestrator.AdvanceAsync(Event("call.hangup", ConsultLeg, TelnyxBridgedTransferTests.ConsultState(answered: false)), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                $"POST /v2/calls/{NumberLeg}/actions/hangup",
                $"GET /v2/calls/{AgentLeg}",
                $"PUT /v2/calls/{AgentLeg}/actions/client_state_update",
            ],
            handler.Requests.Select(Describe));
    }

    [Fact]
    public async Task TheCallerLeftDuringTheConsult_AndItEnds_TheDestinationIsReleased()
    {
        // Arrange - the handover finds nobody to hand over.
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90018"}]}""")
            .RespondWith(HttpStatusCode.OK, Ok)
            .RespondWith(HttpStatusCode.NotFound, """{"errors":[{"code":"90015"}]}""")
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var orchestrator = CreateOrchestrator(handler, []);

        // Act
        await orchestrator.AdvanceAsync(Event("call.hangup", ConsultLeg, TelnyxBridgedTransferTests.ConsultState(answered: true)), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                $"POST /v2/calls/{NumberLeg}/actions/bridge",
                $"POST /v2/calls/{NumberLeg}/actions/hangup",
                $"GET /v2/calls/{AgentLeg}",
                $"POST /v2/calls/{RemoteLeg}/actions/hangup",
            ],
            handler.Requests.Select(Describe));
    }

    [Fact]
    public async Task TheAgentHangsUpTheHeldCallDuringAConsult_TheCallerIsKeptForTheTransfer()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        var orchestrator = CreateOrchestrator(handler, []);

        // Act
        await orchestrator.AdvanceAsync(
            Event("call.hangup", AgentLeg, TelnyxBridgedDialTests.AgentLeg(peer: RemoteLeg).WithPendingTransfer(ConsultLeg)),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(handler.Requests);
    }

    private static TelnyxOutboundBridgeState ConsultTransferLegState()
        => new()
        {
            Intent = TelnyxOutboundBridgeState.TransferLegIntent,
            PeerCallControlId = ConsultLeg,
            TransferOfCallControlId = AgentLeg,
            ConferenceName = $"consult-{ConsultLeg}",
            TargetUserId = "user-2",
        };

    private static TelnyxOutboundBridgeState ConsultNumberState()
        => new()
        {
            Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
            PeerCallControlId = ConsultLeg,
            TransferOfCallControlId = AgentLeg,
        };

    private static string Status(bool alive, TelnyxOutboundBridgeState state)
        => $$$"""{"data":{"call_control_id":"leg","is_alive":{{{(alive ? "true" : "false")}}},"client_state":"{{{state.ToClientState()}}}"}}""";

    private static TelnyxCallEvent Event(string eventType, string callControlId, TelnyxOutboundBridgeState state)
        => new()
        {
            EventType = eventType,
            CallControlId = callControlId,
            ClientState = Encoding.UTF8.GetString(Convert.FromBase64String(state.ToClientState())),
        };

    private static TelnyxOutboundBridgeState State(string body)
    {
        Assert.True(TelnyxOutboundBridgeState.TryParseEncoded(ReadString(body, "client_state"), out var state));

        return state;
    }

    private static string Describe(RecordingHttpMessageHandler.RecordedRequest request)
        => $"{request.Method} {Uri.UnescapeDataString(request.Path)}";

    private static string ReadString(string json, string property)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.GetProperty(property).GetString();
    }

    private static bool HasProperty(string json, string property)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.TryGetProperty(property, out _);
    }

    private static TelnyxOutboundBridgeOrchestrator CreateOrchestrator(RecordingHttpMessageHandler handler, List<TelephonyInteraction> history)
    {
        var options = new TelnyxOptions
        {
            IsEnabled = true,
            ApiKey = "KEY",
            ConnectionId = "connection-1",
            ApiBaseUrl = "https://api.telnyx.com/v2/",
            DefaultOutboundCallerId = "+17785550000",
            OutboundVoiceProfileId = "voice-profile-1",
        };

        var store = new Mock<ITelephonyInteractionStore>();
        store.Setup(x => x.CreateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()))
            .Callback<TelephonyInteraction, CancellationToken>((interaction, _) => history.Add(interaction))
            .Returns(Task.CompletedTask);

        return new(
            new TelnyxApiClient(
                new HttpClient(handler) { BaseAddress = new Uri("https://api.telnyx.com/v2/") },
                new OptionsWrapper<TelnyxOptions>(options),
                new TelnyxApiRetryPolicy(TimeSpan.Zero),
                NullLogger<TelnyxApiClient>.Instance),
            NullLogger<TelnyxOutboundBridgeOrchestrator>.Instance,
            new TestOptionsMonitor<TelnyxOptions>(options),
            new Mock<IContactCenterAgentLegFailureService>().Object,
            [],
            [],
            [],
            [],
            store.Object);
    }
}
