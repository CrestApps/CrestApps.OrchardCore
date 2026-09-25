using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The agent leaving a Telnyx conference while the others stay connected, and the agent ending it for everyone.
/// </summary>
/// <remarks>
/// Live, the agent dialed a number, called extension 2, merged the two and pressed Hang up: everybody was disconnected.
/// The agent's leg had joined the conference with <c>end_conference_on_exit</c> -- "the conference should end and all
/// remaining participants be hung up after the participant leaves" (Telnyx, Join a conference) -- and the phone hung up
/// every one of the agent's legs, each of which released its own party. A phone system's Hang up leaves a conference; the
/// others carry on. Ending it for everyone is its own action, which Telnyx's <c>POST /conferences/{id}/actions/end</c>
/// ("End a conference and terminate all active participants") performs.
/// </remarks>
public sealed class TelnyxConferenceLeaveTests
{
    private const string ExtensionAgentLeg = "ext-agent";
    private const string ColleagueLeg = "colleague-leg";
    private const string CallerLeg = "caller-leg";

    private static readonly string _callerStatus = $$$"""{"data":{"call_control_id":"{{{CallerLeg}}}","is_alive":true}}""";

    [Fact]
    public async Task TheAgentJoinsAConferenceMadeFromADialedNumber_WithoutEndingItOnExit()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxBridgedDialTests.AgentLeg(peer: "remote-a")))
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxBridgedDialTests.AgentLeg(peer: "remote-b")))
            .RespondWith(HttpStatusCode.OK, """{"data":{"id":"conference-1"}}""")
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = TelnyxMergeExtensionCallTests.CreateProvider(handler);

        // Act
        var result = await provider.MergeAsync(new MergeRequest { CallIds = ["agent-a", "agent-b"] }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        var agentJoin = handler.Requests[3];
        Assert.Equal("POST /v2/conferences/conference-1/actions/join", TelnyxMergeExtensionCallTests.Describe(agentJoin));
        Assert.Equal("agent-a", TelnyxMergeExtensionCallTests.ReadString(agentJoin.Body, "call_control_id"));
        Assert.False(HasProperty(agentJoin.Body, "end_conference_on_exit"));
        Assert.DoesNotContain(handler.Requests, request => request.Body?.Contains("end_conference_on_exit", StringComparison.Ordinal) == true);
    }

    // The extension call's own conference holds the agent's leg with end_conference_on_exit, which cannot be changed once
    // joined, so a merge with a dialed number is led by the dialed number, whichever the phone named first.
    [Fact]
    public async Task ADialedNumberLeadsAMergeWithAnExtensionCall_WhateverTheOrder()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxMergeExtensionCallTests.ExtensionAgentState(peer: ColleagueLeg)))
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxBridgedDialTests.AgentLeg(peer: "remote-b")))
            .RespondWith(HttpStatusCode.OK, """{"data":{"id":"conference-1"}}""")
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = TelnyxMergeExtensionCallTests.CreateProvider(handler);

        // Act
        var result = await provider.MergeAsync(new MergeRequest { CallIds = [ExtensionAgentLeg, "keypad-agent"] }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            [
                $"GET /v2/calls/{ExtensionAgentLeg}",
                "GET /v2/calls/keypad-agent",
                "POST /v2/conferences",
                "POST /v2/conferences/conference-1/actions/join",
                "POST /v2/conferences/conference-1/actions/join",
            ],
            handler.Requests.Select(TelnyxMergeExtensionCallTests.Describe));
        Assert.Equal("remote-b", TelnyxMergeExtensionCallTests.ReadString(handler.Requests[2].Body, "call_control_id"));
        Assert.Equal("conf-keypad-agent", TelnyxMergeExtensionCallTests.ReadString(handler.Requests[2].Body, "name"));
        Assert.Equal("keypad-agent", TelnyxMergeExtensionCallTests.ReadString(handler.Requests[3].Body, "call_control_id"));
        Assert.False(HasProperty(handler.Requests[3].Body, "end_conference_on_exit"));
        Assert.Equal(ColleagueLeg, TelnyxMergeExtensionCallTests.ReadString(handler.Requests[4].Body, "call_control_id"));
        Assert.Equal("keypad-agent", result.Call.CallId);
        Assert.Equal("conf-keypad-agent", result.Call.Metadata["conferenceName"]);
    }

    // With no dialed number to lead, the colleague is moved into a new conference -- detached, so their hanging up
    // does not reach back for the agent -- and the agent's leg joins it without ending it on exit. The extension call's own
    // conference is left with nobody in it.
    [Fact]
    public async Task AMergeLedByAnExtensionCall_MovesTheColleagueIntoANewConference_WhichTheAgentLeavesWithoutEndingIt()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, _callerStatus)
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxMergeExtensionCallTests.ExtensionAgentState(peer: ColleagueLeg)))
            .RespondWith(HttpStatusCode.OK, """{"data":{"id":"conference-1"}}""")
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = TelnyxMergeExtensionCallTests.CreateProvider(handler);

        // Act
        var result = await provider.MergeAsync(new MergeRequest { CallIds = [CallerLeg, ExtensionAgentLeg] }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            [
                $"GET /v2/calls/{CallerLeg}",
                $"GET /v2/calls/{ExtensionAgentLeg}",
                "POST /v2/conferences",
                "POST /v2/conferences/conference-1/actions/join",
                "POST /v2/conferences/conference-1/actions/join",
            ],
            handler.Requests.Select(TelnyxMergeExtensionCallTests.Describe));

        var create = handler.Requests[2];
        Assert.Equal(ColleagueLeg, TelnyxMergeExtensionCallTests.ReadString(create.Body, "call_control_id"));
        Assert.Equal($"conf-{ExtensionAgentLeg}", TelnyxMergeExtensionCallTests.ReadString(create.Body, "name"));
        Assert.True(TelnyxOutboundBridgeState.TryParseEncoded(TelnyxMergeExtensionCallTests.ReadString(create.Body, "client_state"), out var colleague));
        Assert.True(colleague.Detached);
        Assert.Null(colleague.VoicemailRecipientUserId);

        Assert.Equal(ExtensionAgentLeg, TelnyxMergeExtensionCallTests.ReadString(handler.Requests[3].Body, "call_control_id"));
        Assert.False(HasProperty(handler.Requests[3].Body, "end_conference_on_exit"));
        Assert.Equal(CallerLeg, TelnyxMergeExtensionCallTests.ReadString(handler.Requests[4].Body, "call_control_id"));
        Assert.Equal(ExtensionAgentLeg, result.Call.CallId);
        Assert.Equal($"conf-{ExtensionAgentLeg}", result.Call.Metadata["conferenceName"]);
    }

    [Fact]
    public async Task WhenTelnyxRefusesToMoveTheColleague_TheMergeUsesTheExtensionCallsOwnConference()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, _callerStatus)
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxMergeExtensionCallTests.ExtensionAgentState(peer: ColleagueLeg)))
            .RespondWith(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90040","title":"Conference create not allowed"}]}""")
            .RespondWith(HttpStatusCode.OK, $$$"""{"data":[{"id":"conference-ext","name":"ext-{{{ExtensionAgentLeg}}}"}]}""")
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = TelnyxMergeExtensionCallTests.CreateProvider(handler);

        // Act
        var result = await provider.MergeAsync(new MergeRequest { CallIds = [CallerLeg, ExtensionAgentLeg] }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            [
                $"GET /v2/calls/{CallerLeg}",
                $"GET /v2/calls/{ExtensionAgentLeg}",
                "POST /v2/conferences",
                $"GET /v2/conferences?filter[name]=ext-{ExtensionAgentLeg}",
                $"PUT /v2/calls/{ColleagueLeg}/actions/client_state_update",
                "POST /v2/conferences/conference-ext/actions/join",
            ],
            handler.Requests.Select(TelnyxMergeExtensionCallTests.Describe));
        Assert.Equal($"ext-{ExtensionAgentLeg}", result.Call.Metadata["conferenceName"]);
    }

    [Fact]
    public async Task LeavingADialedNumbersCall_DetachesItsLegs_AndHangsUpOnlyTheAgentsLeg()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxBridgedDialTests.AgentLeg(peer: "remote-a")))
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = TelnyxMergeExtensionCallTests.CreateProvider(handler);

        // Act
        var result = await provider.HangupAsync(Leave("agent-a"), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            [
                "GET /v2/calls/agent-a",
                "PUT /v2/calls/agent-a/actions/client_state_update",
                "PUT /v2/calls/remote-a/actions/client_state_update",
                "POST /v2/calls/agent-a/actions/hangup",
            ],
            handler.Requests.Select(TelnyxMergeExtensionCallTests.Describe));

        // The agent's leg ending no longer releases the party, and the party's end no longer reaches back for it.
        Assert.True(TelnyxOutboundBridgeState.TryParseEncoded(TelnyxMergeExtensionCallTests.ReadString(handler.Requests[1].Body, "client_state"), out var agent));
        Assert.True(agent.Detached);
        Assert.Equal("remote-a", agent.PeerCallControlId);
        Assert.True(TelnyxOutboundBridgeState.TryParseEncoded(TelnyxMergeExtensionCallTests.ReadString(handler.Requests[2].Body, "client_state"), out var party));
        Assert.True(party.Detached);
        Assert.Equal(CallState.Disconnected, result.Call.State);
        Assert.DoesNotContain(handler.Requests, request => request.Path.Contains("remote-a/actions/hangup", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LeavingAnExtensionCall_DetachesTheColleague_AndHangsUpOnlyTheAgentsLeg()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxMergeExtensionCallTests.ExtensionAgentState(peer: ColleagueLeg)))
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = TelnyxMergeExtensionCallTests.CreateProvider(handler);

        // Act
        var result = await provider.HangupAsync(Leave(ExtensionAgentLeg), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            [
                $"GET /v2/calls/{ExtensionAgentLeg}",
                $"PUT /v2/calls/{ExtensionAgentLeg}/actions/client_state_update",
                $"PUT /v2/calls/{ColleagueLeg}/actions/client_state_update",
                $"POST /v2/calls/{ExtensionAgentLeg}/actions/hangup",
            ],
            handler.Requests.Select(TelnyxMergeExtensionCallTests.Describe));
        Assert.True(TelnyxOutboundBridgeState.TryParseEncoded(TelnyxMergeExtensionCallTests.ReadString(handler.Requests[2].Body, "client_state"), out var colleague));
        Assert.True(colleague.Detached);
        Assert.Null(colleague.VoicemailRecipientUserId);
    }

    // A caller's own leg is the caller, not the agent: leaving never hangs it up.
    [Fact]
    public async Task LeavingACallersCall_HangsUpNothing()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, _callerStatus)
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = TelnyxMergeExtensionCallTests.CreateProvider(handler);

        // Act
        var result = await provider.HangupAsync(Leave(CallerLeg), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.Equal(CallState.Connected, result.Call.State);
    }

    // Hung up still attached, the agent's leg would release its party with it: the agent leaving must never be what
    // disconnects somebody else.
    [Fact]
    public async Task WhenTheAgentsLegCannotBeDetached_LeavingRefuses_WithoutHangingAnythingUp()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxBridgedDialTests.AgentLeg(peer: "remote-a")))
            .AlwaysRespondWith(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90018","title":"Call has already ended"}]}""");
        var provider = TelnyxMergeExtensionCallTests.CreateProvider(handler);

        // Act
        var result = await provider.HangupAsync(Leave("agent-a"), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.DoesNotContain(handler.Requests, request => request.Path.EndsWith("/actions/hangup", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EndingTheConferenceForEveryone_EndsItByName_ThenHangsUpTheCall()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, """{"data":[{"id":"conference-1","name":"conf-agent-a"}]}""")
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = TelnyxMergeExtensionCallTests.CreateProvider(handler);

        // Act
        var result = await provider.HangupAsync(EndForEveryone("agent-a", "conf-agent-a"), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            [
                "GET /v2/conferences?filter[name]=conf-agent-a",
                "POST /v2/conferences/conference-1/actions/end",
                "POST /v2/calls/agent-a/actions/hangup",
            ],
            handler.Requests.Select(TelnyxMergeExtensionCallTests.Describe));
        Assert.Equal(CallState.Disconnected, result.Call.State);
    }

    [Fact]
    public async Task EndingAConferenceThatIsNoLongerRunning_StillHangsUpTheCall()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, """{"data":[]}""")
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = TelnyxMergeExtensionCallTests.CreateProvider(handler);

        // Act
        var result = await provider.HangupAsync(EndForEveryone("agent-a", "conf-agent-a"), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            ["GET /v2/conferences?filter[name]=conf-agent-a", "POST /v2/calls/agent-a/actions/hangup"],
            handler.Requests.Select(TelnyxMergeExtensionCallTests.Describe));
    }

    private static CallReference Leave(string callId)
        => new()
        {
            CallId = callId,
            Metadata = new Dictionary<string, object>
            {
                [TelephonyConstants.RequestMetadata.ConferenceLeave] = "true",
                ["isConference"] = true,
            },
        };

    private static CallReference EndForEveryone(string callId, string conferenceName)
        => new()
        {
            CallId = callId,
            Metadata = new Dictionary<string, object>
            {
                [TelephonyConstants.RequestMetadata.ConferenceEnd] = "true",
                ["conferenceName"] = conferenceName,
            },
        };

    private static bool HasProperty(string json, string property)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.TryGetProperty(property, out _);
    }
}
