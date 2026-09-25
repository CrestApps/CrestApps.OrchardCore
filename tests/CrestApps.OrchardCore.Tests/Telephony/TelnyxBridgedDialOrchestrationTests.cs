using System.Net;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The webhook side of a number dialed from the soft phone and connected on the server: the dialed party's leg is
/// written onto the agent's leg, the two are bridged the way a Contact Center agent leg is, and either one ending takes
/// the other with it -- unless the platform moved it elsewhere first.
/// </summary>
public sealed class TelnyxBridgedDialOrchestrationTests
{
    private const string AgentLeg = "agent-leg-1";
    private const string RemoteLeg = "remote-leg-1";

    [Fact]
    public async Task AgentLegAnswered_DialsTheNumber_AndRecordsItsLegOnTheAgentLeg()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, $$$"""{"data":{"call_control_id":"{{{RemoteLeg}}}"}}""")
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var orchestrator = CreateOrchestrator(handler);

        // Act
        await orchestrator.AdvanceAsync(Event("call.answered", AgentLeg, AgentLegState()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ["POST /v2/calls", $"PUT /v2/calls/{AgentLeg}/actions/client_state_update"],
            handler.Requests.Select(Describe));
        Assert.Equal("+17025550101", ReadString(handler.Requests[0].Body, "to"));
        Assert.Equal("voice-profile-1", ReadString(handler.Requests[0].Body, "outbound_voice_profile_id"));

        Assert.True(TelnyxOutboundBridgeState.TryParseEncoded(ReadString(handler.Requests[1].Body, "client_state"), out var recorded));
        Assert.Equal(TelnyxOutboundBridgeState.AgentLegIntent, recorded.Intent);
        Assert.Equal(RemoteLeg, recorded.PeerCallControlId);
        Assert.True(recorded.IsBridgedDialAgentLeg);
    }

    [Fact]
    public async Task AgentLegAnsweredOnAnExtensionCall_KeepsItsState()
    {
        // Arrange - an extension call is joined through a conference, by its own rules.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, $$$"""{"data":{"call_control_id":"{{{RemoteLeg}}}"}}""");
        var orchestrator = CreateOrchestrator(handler);
        var state = AgentLegState();
        state.VoicemailRecipientUserId = "user-2";

        // Act
        await orchestrator.AdvanceAsync(Event("call.answered", AgentLeg, state), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["POST /v2/calls"], handler.Requests.Select(Describe));
    }

    [Fact]
    public async Task NumberThatCannotBeDialed_HangsUpTheAgentsSilentLeg()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"10001"}]}""")
            .RespondWith(HttpStatusCode.OK, CallStatus(AgentLegState()))
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var orchestrator = CreateOrchestrator(handler);

        // Act
        await orchestrator.AdvanceAsync(Event("call.answered", AgentLeg, AgentLegState()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ["POST /v2/calls", $"GET /v2/calls/{AgentLeg}", $"POST /v2/calls/{AgentLeg}/actions/hangup"],
            handler.Requests.Select(Describe));
    }

    [Fact]
    public async Task RedeliveredAnswerWhoseDialIsRefusedAsARepeat_LeavesTheConnectedCallUp()
    {
        // Arrange - the leg already names the dialed party: the first delivery dialed it.
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90061"}]}""")
            .RespondWith(HttpStatusCode.OK, CallStatus(AgentLegState().WithPeer(RemoteLeg)));
        var orchestrator = CreateOrchestrator(handler);

        // Act
        await orchestrator.AdvanceAsync(Event("call.answered", AgentLeg, AgentLegState()), TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(handler.Requests, request => request.Path.EndsWith("/actions/hangup", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NumberAnswered_IsBridgedOnTheAgentLeg_WhichParksWhenTheNumberLeaves()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var orchestrator = CreateOrchestrator(handler);

        // Act
        var leg = await orchestrator.AdvanceAsync(Event("call.answered", RemoteLeg, RemoteLegState()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelnyxOutboundBridgeLeg.DestinationLeg, leg);
        var bridge = Assert.Single(handler.Requests);
        Assert.Equal($"POST /v2/calls/{AgentLeg}/actions/bridge", Describe(bridge));
        Assert.Equal(RemoteLeg, ReadString(bridge.Body, "call_control_id"));
        Assert.Equal("self", ReadString(bridge.Body, "park_after_unbridge"));
    }

    // Also while the number is still ringing, when there is no bridge yet to take it down.
    [Fact]
    public async Task AgentHangsUp_TheDialedPartyGoesToo()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var orchestrator = CreateOrchestrator(handler);

        // Act
        var leg = await orchestrator.AdvanceAsync(Event("call.hangup", AgentLeg, AgentLegState().WithPeer(RemoteLeg)), TestContext.Current.CancellationToken);

        // Assert - the agent leg's own hang-up still settles the call the soft phone shows.
        Assert.Equal(TelnyxOutboundBridgeLeg.AgentLeg, leg);
        Assert.Equal([$"POST /v2/calls/{RemoteLeg}/actions/hangup"], handler.Requests.Select(Describe));
    }

    [Fact]
    public async Task AgentLegReleasedAfterATransfer_LeavesTheTransferredPartyAlone()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        var orchestrator = CreateOrchestrator(handler);

        // Act
        await orchestrator.AdvanceAsync(Event("call.hangup", AgentLeg, AgentLegState().WithPeer(RemoteLeg).AsDetached()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("NORMAL_CLEARING")]
    [InlineData("USER_BUSY")]
    [InlineData("NO_ANSWER")]
    public async Task DialedPartyHangsUp_TheAgentsLegIsHungUp(string cause)
    {
        // Arrange - bridged with park_after_unbridge, nothing else ends the agent's leg.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var orchestrator = CreateOrchestrator(handler);
        var hangup = Event("call.hangup", RemoteLeg, RemoteLegState());
        hangup.HangupCause = cause;

        // Act
        await orchestrator.AdvanceAsync(hangup, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([$"POST /v2/calls/{AgentLeg}/actions/hangup"], handler.Requests.Select(Describe));
    }

    [Fact]
    public async Task DialedPartyMovedIntoAConferenceOrTransferred_HangingUp_LeavesTheAgentsLegAlone()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        var orchestrator = CreateOrchestrator(handler);

        // Act
        await orchestrator.AdvanceAsync(Event("call.hangup", RemoteLeg, RemoteLegState().AsDetached()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(handler.Requests);
    }

    private static TelnyxOutboundBridgeState AgentLegState()
        => new()
        {
            Intent = TelnyxOutboundBridgeState.AgentLegIntent,
            Destination = "+17025550101",
            CallerId = "+17785550000",
        };

    private static TelnyxOutboundBridgeState RemoteLegState()
        => new()
        {
            Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
            PeerCallControlId = AgentLeg,
        };

    private static string CallStatus(TelnyxOutboundBridgeState state)
        => $$$"""{"data":{"call_control_id":"{{{AgentLeg}}}","is_alive":true,"client_state":"{{{state.ToClientState()}}}"}}""";

    // The webhook parser hands the orchestrator client state already decoded.
    private static TelnyxCallEvent Event(string eventType, string callControlId, TelnyxOutboundBridgeState state)
        => new()
        {
            EventType = eventType,
            CallControlId = callControlId,
            ClientState = Encoding.UTF8.GetString(Convert.FromBase64String(state.ToClientState())),
        };

    private static string Describe(RecordingHttpMessageHandler.RecordedRequest request)
        => $"{request.Method} {Uri.UnescapeDataString(request.Path)}";

    private static string ReadString(string json, string property)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.GetProperty(property).GetString();
    }

    private static TelnyxOutboundBridgeOrchestrator CreateOrchestrator(RecordingHttpMessageHandler handler)
    {
        var options = new TelnyxOptions
        {
            IsEnabled = true,
            ApiKey = "KEY",
            ConnectionId = "connection-1",
            ApiBaseUrl = "https://api.telnyx.com/v2/",
            OutboundVoiceProfileId = "voice-profile-1",
        };

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
            []);
    }
}
