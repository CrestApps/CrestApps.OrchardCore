using System.Net;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Merging an internal extension call with the other calls on the soft phone.
/// </summary>
/// <remarks>
/// An extension call is already a conference: the agent's leg is in <c>ext-{agent leg}</c> with the colleague, and
/// leaving it (<c>end_conference_on_exit</c>) ends it for the colleague. A merge that made a new conference from the
/// agent's leg pulled it out of that one, which hung the colleague up; the colleague's hang-up then hung up the agent's
/// leg, and the dialed party was left alone in a conference nobody else was in. Nobody could hear anybody.
/// </remarks>
public sealed class TelnyxMergeExtensionCallTests
{
    private const string ExtensionAgentLeg = "ext-agent";
    private const string ColleagueLeg = "colleague-leg";
    private const string KeypadAgentLeg = "keypad-agent";
    private const string RemoteLeg = "remote-b";

    [Fact]
    public async Task MergingAnExtensionCallFirst_JoinsTheOthersToItsOwnConference_WithoutMovingTheAgentsLeg()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(ExtensionAgentState(peer: ColleagueLeg)))
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxBridgedDialTests.AgentLeg(peer: RemoteLeg)))
            .RespondWith(HttpStatusCode.OK, $$$"""{"data":[{"id":"conference-ext","name":"ext-{{{ExtensionAgentLeg}}}"}]}""")
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.MergeAsync(new MergeRequest { CallIds = [ExtensionAgentLeg, KeypadAgentLeg] }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(
            [
                $"GET /v2/calls/{ExtensionAgentLeg}",
                $"GET /v2/calls/{KeypadAgentLeg}",
                $"GET /v2/conferences?filter[name]=ext-{ExtensionAgentLeg}",
                $"PUT /v2/calls/{ColleagueLeg}/actions/client_state_update",
                "POST /v2/conferences/conference-ext/actions/join",
            ],
            handler.Requests.Select(Describe));

        // The colleague is now one participant among several: hanging up must not end the agent's leg (and with it the
        // conference) for everybody else.
        Assert.True(TelnyxOutboundBridgeState.TryParseEncoded(ReadString(handler.Requests[3].Body, "client_state"), out var colleague));
        Assert.True(colleague.Detached);
        Assert.Null(colleague.VoicemailRecipientUserId);

        Assert.Equal(RemoteLeg, ReadString(handler.Requests[4].Body, "call_control_id"));
        Assert.Equal($"ext-{ExtensionAgentLeg}", result.Call.Metadata["conferenceName"]);
    }

    [Fact]
    public async Task MergingAnExtensionCallSecond_JoinsTheColleague_AndLeavesTheAgentsExtensionLegAlone()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxBridgedDialTests.AgentLeg(peer: RemoteLeg)))
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(ExtensionAgentState(peer: ColleagueLeg)))
            .RespondWith(HttpStatusCode.OK, """{"data":{"id":"conference-1"}}""")
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.MergeAsync(new MergeRequest { CallIds = [KeypadAgentLeg, ExtensionAgentLeg] }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(
            [
                $"GET /v2/calls/{KeypadAgentLeg}",
                $"GET /v2/calls/{ExtensionAgentLeg}",
                "POST /v2/conferences",
                "POST /v2/conferences/conference-1/actions/join",
                "POST /v2/conferences/conference-1/actions/join",
            ],
            handler.Requests.Select(Describe));
        Assert.Equal(RemoteLeg, ReadString(handler.Requests[2].Body, "call_control_id"));
        Assert.Equal(KeypadAgentLeg, ReadString(handler.Requests[3].Body, "call_control_id"));
        Assert.Equal(ColleagueLeg, ReadString(handler.Requests[4].Body, "call_control_id"));
        Assert.DoesNotContain(handler.Requests, request => request.Body?.Contains(ExtensionAgentLeg, StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task MergingAnExtensionCallWhoseColleagueIsNotConnected_IsRefused_BeforeAnythingMoves(int position)
    {
        // Arrange
        var extension = TelnyxBridgedDialTests.CallStatus(ExtensionAgentState(peer: null));
        var keypad = TelnyxBridgedDialTests.CallStatus(TelnyxBridgedDialTests.AgentLeg(peer: RemoteLeg));
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, position == 0 ? extension : keypad)
            .RespondWith(HttpStatusCode.OK, position == 0 ? keypad : extension)
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = CreateProvider(handler);
        string[] callIds = position == 0 ? [ExtensionAgentLeg, KeypadAgentLeg] : [KeypadAgentLeg, ExtensionAgentLeg];

        // Act
        var result = await provider.MergeAsync(new MergeRequest { CallIds = callIds }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    [Fact]
    public async Task MergingAnExtensionCallWhoseConferenceHasEnded_IsRefused_WithoutMakingAnother()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(ExtensionAgentState(peer: ColleagueLeg)))
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxBridgedDialTests.AgentLeg(peer: RemoteLeg)))
            .RespondWith(HttpStatusCode.OK, """{"data":[]}""")
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.MergeAsync(new MergeRequest { CallIds = [ExtensionAgentLeg, KeypadAgentLeg] }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    [Fact]
    public async Task ExtensionAgentLegAnswered_RecordsTheColleaguesLegOnTheAgentLeg()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, $$$"""{"data":{"call_control_id":"{{{ColleagueLeg}}}"}}""")
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var orchestrator = CreateOrchestrator(handler);

        // Act
        await orchestrator.AdvanceAsync(Event("call.answered", ExtensionAgentLeg, ExtensionAgentState(peer: null)), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["POST /v2/calls", $"PUT /v2/calls/{ExtensionAgentLeg}/actions/client_state_update"], handler.Requests.Select(Describe));
        Assert.True(TelnyxOutboundBridgeState.TryParseEncoded(ReadString(handler.Requests[1].Body, "client_state"), out var recorded));
        Assert.Equal(ColleagueLeg, recorded.PeerCallControlId);
        Assert.Equal("user-2", recorded.VoicemailRecipientUserId);
        Assert.False(recorded.IsBridgedDialAgentLeg);
    }

    // A merged extension call's colleague is in the merge conference, not in the agent leg's own conference, so the
    // agent hanging up that row would otherwise leave them on the line.
    [Fact]
    public async Task ExtensionAgentLegHangsUp_TheColleaguesLegGoesToo()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var orchestrator = CreateOrchestrator(handler);

        // Act
        await orchestrator.AdvanceAsync(Event("call.hangup", ExtensionAgentLeg, ExtensionAgentState(peer: ColleagueLeg)), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([$"POST /v2/calls/{ColleagueLeg}/actions/hangup"], handler.Requests.Select(Describe));
    }

    [Fact]
    public async Task DetachedExtensionAgentLegHangsUp_LeavesTheColleagueAlone()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        var orchestrator = CreateOrchestrator(handler);

        // Act
        await orchestrator.AdvanceAsync(Event("call.hangup", ExtensionAgentLeg, ExtensionAgentState(peer: ColleagueLeg).AsDetached()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ColleagueMergedIntoTheAgentsConferenceHangsUp_TheConferenceGoesOn()
    {
        // Arrange - the merge detached the colleague from the agent's leg.
        var handler = new RecordingHttpMessageHandler();
        var orchestrator = CreateOrchestrator(handler);
        var hangup = Event("call.hangup", ColleagueLeg, new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
            PeerCallControlId = ExtensionAgentLeg,
            Detached = true,
        });
        hangup.HangupCause = "NORMAL_CLEARING";

        // Act
        await orchestrator.AdvanceAsync(hangup, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ColleagueOfAnExtensionCallHangsUp_TheCallersLegIsStillHungUp()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var orchestrator = CreateOrchestrator(handler);
        var hangup = Event("call.hangup", ColleagueLeg, new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
            PeerCallControlId = ExtensionAgentLeg,
            VoicemailRecipientUserId = "user-2",
        });
        hangup.HangupCause = "NORMAL_CLEARING";

        // Act
        await orchestrator.AdvanceAsync(hangup, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([$"POST /v2/calls/{ExtensionAgentLeg}/actions/hangup"], handler.Requests.Select(Describe));
    }

    internal static TelnyxOutboundBridgeState ExtensionAgentState(string peer)
        => new()
        {
            Intent = TelnyxOutboundBridgeState.AgentLegIntent,
            Destination = "sip:gencred2@sip.telnyx.com",
            CallerId = "+17785550000",
            VoicemailRecipientUserId = "user-2",
            RingTimeoutSeconds = 25,
            PeerCallControlId = peer,
        };

    private static TelnyxCallEvent Event(string eventType, string callControlId, TelnyxOutboundBridgeState state)
        => new()
        {
            EventType = eventType,
            CallControlId = callControlId,
            ClientState = Encoding.UTF8.GetString(Convert.FromBase64String(state.ToClientState())),
        };

    internal static string Describe(RecordingHttpMessageHandler.RecordedRequest request)
        => $"{request.Method} {Uri.UnescapeDataString(request.Path)}";

    internal static string ReadString(string json, string property)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.GetProperty(property).GetString();
    }

    private static TelnyxOptions Options()
        => new()
        {
            IsEnabled = true,
            ApiBaseUrl = "https://api.telnyx.com/v2/",
            ApiKey = "test-api-key",
            ConnectionId = "test-connection",
            DefaultOutboundCallerId = "+17785550000",
            OutboundVoiceProfileId = "voice-profile-1",
        };

    private static TelnyxApiClient ApiClient(RecordingHttpMessageHandler handler, TelnyxOptions options)
        => new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.telnyx.com/v2/") },
            new OptionsWrapper<TelnyxOptions>(options),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);

    internal static TelnyxTelephonyProvider CreateProvider(RecordingHttpMessageHandler handler)
    {
        var options = Options();
        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        return new TelnyxTelephonyProvider(
            ApiClient(handler, options),
            new Mock<ITelnyxAgentCredentialStore>().Object,
            new Mock<ITelnyxAgentEndpointResolver>().Object,
            clock.Object,
            NullLogger<TelnyxTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<TelnyxTelephonyProvider>(),
            new TestOptionsMonitor<TelnyxOptions>(options));
    }

    private static TelnyxOutboundBridgeOrchestrator CreateOrchestrator(RecordingHttpMessageHandler handler)
    {
        var options = Options();

        return new(
            ApiClient(handler, options),
            NullLogger<TelnyxOutboundBridgeOrchestrator>.Instance,
            new TestOptionsMonitor<TelnyxOptions>(options),
            new Mock<IContactCenterAgentLegFailureService>().Object,
            [],
            [],
            []);
    }
}
