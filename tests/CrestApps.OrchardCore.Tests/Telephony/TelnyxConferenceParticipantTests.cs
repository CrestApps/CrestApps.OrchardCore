using System.Net;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// A Telnyx conference after the merge that made it: merging its calls again, the order a merge takes its calls in, and
/// hanging up one participant.
/// </summary>
/// <remarks>
/// Live, a merge of a Contact Center caller with an extension call worked; pressing Merge again asked Telnyx to join the
/// caller to the conference it was already in, which it refused ("Participant must not join the same conference twice"),
/// and the phone said the merge had failed. Then dropping the colleague meant hanging up the extension call's own leg --
/// the agent's way into the conference, joined with <c>end_conference_on_exit</c> -- which ended it for the caller too.
/// </remarks>
public sealed class TelnyxConferenceParticipantTests
{
    private const string ExtensionAgentLeg = "ext-agent";
    private const string ColleagueLeg = "colleague-leg";
    private const string CallerLeg = "caller-leg";

    private const string AlreadyJoined =
        """{"errors":[{"code":"90044","title":"Conference join not allowed","detail":"Participant must not join the same conference twice."}]}""";

    // A Contact Center caller's own leg: it carries no state of the soft phone's.
    private static readonly string _callerStatus = $$$"""{"data":{"call_control_id":"{{{CallerLeg}}}","is_alive":true}}""";

    [Fact]
    public async Task MergingCallsAlreadyInTheConferenceAgain_Succeeds_WithoutChangingAnything()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxMergeExtensionCallTests.ExtensionAgentState(peer: ColleagueLeg)))
            .RespondWith(HttpStatusCode.OK, _callerStatus)
            .RespondWith(HttpStatusCode.OK, """{"data":[]}""")
            .RespondWith(HttpStatusCode.OK, """{"data":{"id":"conference-1"}}""")
            .RespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""")
            .RespondWith(HttpStatusCode.UnprocessableEntity, AlreadyJoined);
        var provider = TelnyxMergeExtensionCallTests.CreateProvider(handler);

        // Act
        var result = await provider.MergeAsync(new MergeRequest { CallIds = [ExtensionAgentLeg, CallerLeg] }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(ExtensionAgentLeg, result.Call.CallId);
        Assert.Equal(true, result.Call.Metadata["isConference"]);
    }

    [Fact]
    public async Task AJoinRefusedForAnyOtherReason_StillFailsTheMerge()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxMergeExtensionCallTests.ExtensionAgentState(peer: ColleagueLeg)))
            .RespondWith(HttpStatusCode.OK, _callerStatus)
            .RespondWith(HttpStatusCode.OK, """{"data":[]}""")
            .RespondWith(HttpStatusCode.OK, """{"data":{"id":"conference-1"}}""")
            .RespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""")
            .RespondWith(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90018","title":"Call has already ended"}]}""");
        var provider = TelnyxMergeExtensionCallTests.CreateProvider(handler);

        // Act
        var result = await provider.MergeAsync(new MergeRequest { CallIds = [ExtensionAgentLeg, CallerLeg] }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task HangingUpTheParticipantOfTheCallTheConferenceWasMadeFrom_HangsUpOnlyTheColleague()
    {
        // Arrange - the merge detached the colleague from the agent's leg, which stays in the conference.
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxMergeExtensionCallTests.ExtensionAgentState(peer: ColleagueLeg)))
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(ColleagueState(detached: true)))
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = TelnyxMergeExtensionCallTests.CreateProvider(handler);

        // Act
        var result = await provider.HangupAsync(ParticipantHangup(ExtensionAgentLeg), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            [$"GET /v2/calls/{ExtensionAgentLeg}", $"GET /v2/calls/{ColleagueLeg}", $"POST /v2/calls/{ColleagueLeg}/actions/hangup"],
            handler.Requests.Select(TelnyxMergeExtensionCallTests.Describe));
        Assert.Equal(ExtensionAgentLeg, result.Call.CallId);
        Assert.Equal(CallState.Connected, result.Call.State);
        Assert.Equal(true, result.Call.Metadata[TelephonyConstants.CallMetadata.ParticipantLeft]);
    }

    // Any other merged call's agent leg is parked outside the conference, and hanging it up releases its party.
    [Fact]
    public async Task HangingUpAParticipantWhoseAgentLegIsParked_HangsUpTheAgentLeg()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxMergeExtensionCallTests.ExtensionAgentState(peer: ColleagueLeg)))
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(ColleagueState(detached: false)))
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = TelnyxMergeExtensionCallTests.CreateProvider(handler);

        // Act
        var result = await provider.HangupAsync(ParticipantHangup(ExtensionAgentLeg), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal($"POST /v2/calls/{ExtensionAgentLeg}/actions/hangup", TelnyxMergeExtensionCallTests.Describe(handler.Requests[^1]));
        Assert.DoesNotContain(handler.Requests, request => request.Path.Contains($"{ColleagueLeg}/actions", StringComparison.Ordinal));
        Assert.Equal(CallState.Disconnected, result.Call.State);
    }

    [Fact]
    public async Task HangingUpACallersRowInTheConference_HangsUpTheCaller()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, _callerStatus)
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = TelnyxMergeExtensionCallTests.CreateProvider(handler);

        // Act
        var result = await provider.HangupAsync(ParticipantHangup(CallerLeg), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal($"POST /v2/calls/{CallerLeg}/actions/hangup", TelnyxMergeExtensionCallTests.Describe(handler.Requests[^1]));
        Assert.Equal(CallState.Disconnected, result.Call.State);
    }

    [Fact]
    public async Task AnOrdinaryHangup_HangsUpTheCallItNames_WithoutReadingItFirst()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = TelnyxMergeExtensionCallTests.CreateProvider(handler);

        // Act
        var result = await provider.HangupAsync(new CallReference { CallId = ExtensionAgentLeg }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal([$"POST /v2/calls/{ExtensionAgentLeg}/actions/hangup"], handler.Requests.Select(TelnyxMergeExtensionCallTests.Describe));
    }

    private static CallReference ParticipantHangup(string callId)
        => new()
        {
            CallId = callId,
            Metadata = new Dictionary<string, object>
            {
                [TelephonyConstants.RequestMetadata.ConferenceParticipant] = "true",
                [TelephonyConstants.CallMetadata.ExtensionNumber] = "2",
            },
        };

    private static TelnyxOutboundBridgeState ColleagueState(bool detached)
        => new()
        {
            Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
            PeerCallControlId = ExtensionAgentLeg,
            VoicemailRecipientUserId = detached ? null : "user-2",
            Detached = detached,
        };
}
