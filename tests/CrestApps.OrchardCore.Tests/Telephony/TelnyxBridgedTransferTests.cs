using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Transferring a number dialed from the soft phone, blind and warm, as the provider starts, follows, completes and
/// cancels it.
/// </summary>
/// <remarks>
/// A blind transfer used to send the dialed party to the colleague with <c>actions/transfer</c>. The colleague's phone
/// got a leg nobody had recorded -- no client state naming the party, nothing in their history -- so they could not
/// transfer the call again, and a colleague who did not answer left the party alone on a parked line. A warm transfer
/// was refused outright. Now the destination is rung on a leg of the platform's own and the party is handed over only
/// when it answers; a warm transfer rings the agent's own phone for a consult first.
/// </remarks>
public sealed class TelnyxBridgedTransferTests
{
    private const string AgentLeg = "agent-leg-1";
    private const string RemoteLeg = "remote-leg-1";
    private const string TransferLeg = "xfer-leg-1";
    private const string ConsultLeg = "consult-leg-1";
    private const string ColleagueLeg = "colleague-leg-1";
    private const string Number = "+17025550101";
    private const string CallerId = "+17785550000";
    private const string ColleagueEndpoint = "sip:gencred2@sip.telnyx.com";
    private const string Ok = """{"data":{"result":"ok"}}""";

    [Fact]
    public async Task BlindTransferToAnExtension_RingsTheColleague_AndLeavesTheCallerWithTheAgentUntilTheyAnswer()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxBridgedDialTests.AgentLeg(peer: RemoteLeg)))
            .RespondWith(HttpStatusCode.OK, $$$"""{"data":{"call_control_id":"{{{TransferLeg}}}"}}""")
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var history = new List<TelephonyInteraction>();
        var provider = CreateProvider(handler, history);

        // Act
        var result = await provider.TransferAsync(ExtensionTransfer(TransferMode.Blind), TestContext.Current.CancellationToken);

        // Assert - nothing moves the caller yet: no transfer, no bridge, no hang-up.
        Assert.True(result.Succeeded);
        Assert.Equal(
            [
                $"GET /v2/calls/{AgentLeg}",
                "POST /v2/calls",
                $"PUT /v2/calls/{AgentLeg}/actions/client_state_update",
                $"PUT /v2/calls/{RemoteLeg}/actions/client_state_update",
            ],
            handler.Requests.Select(Describe));

        var ring = handler.Requests[1];
        Assert.Equal(ColleagueEndpoint, ReadString(ring.Body, "to"));
        Assert.Equal(CallerId, ReadString(ring.Body, "from"));
        Assert.Equal(Number, ReadString(ring.Body, "from_display_name"));
        Assert.Equal(30, ReadInt(ring.Body, "timeout_secs"));
        Assert.False(HasProperty(ring.Body, "outbound_voice_profile_id"));
        Assert.Contains(TelnyxTransferCommands.TransferLegSipHeader, ring.Body, StringComparison.Ordinal);

        var leg = State(ring.Body);
        Assert.Equal(TelnyxOutboundBridgeState.TransferLegIntent, leg.Intent);
        Assert.Equal(RemoteLeg, leg.PeerCallControlId);
        Assert.Equal(AgentLeg, leg.TransferOfCallControlId);
        Assert.Equal("user-2", leg.TargetUserId);
        Assert.Equal(Number, leg.PartyNumber);
        Assert.Equal("Agent One", leg.CallerDisplayName);
        Assert.Null(leg.ConferenceName);

        // While it rings, the agent hanging up keeps the caller for the transfer, and the caller hanging up releases the
        // leg ringing for them.
        var agent = State(handler.Requests[2].Body);
        Assert.Equal(TransferLeg, agent.PendingTransferCallControlId);
        Assert.Equal(RemoteLeg, agent.PeerCallControlId);
        var party = State(handler.Requests[3].Body);
        Assert.Equal(TransferLeg, party.PendingTransferCallControlId);
        Assert.Equal(AgentLeg, party.PeerCallControlId);

        // The colleague's phone may act on the leg only because it is in their history, ringing.
        var recorded = Assert.Single(history);
        Assert.Equal(TransferLeg, recorded.CallId);
        Assert.Equal("user-2", recorded.UserId);
        Assert.Equal(Number, recorded.From);
        Assert.Equal(CallDirection.Inbound, recorded.Direction);
        Assert.True(recorded.AwaitingAnswer);
        Assert.Equal(CallOutcome.InProgress, recorded.Outcome);

        // The agent's call is held on the phone while the transfer rings, and the phone follows the transfer's leg.
        Assert.Equal(AgentLeg, result.Call.CallId);
        Assert.True(result.Call.IsOnHold);
        Assert.Equal(TransferLeg, result.Call.Metadata[TelephonyConstants.CallMetadata.ConsultId]);
        Assert.Equal(TelephonyConstants.ConsultStatuses.Ringing, result.Call.Metadata[TelephonyConstants.CallMetadata.ConsultStatus]);
    }

    [Fact]
    public async Task BlindTransferToANumber_RingsItThroughTheOutboundProfile_AndRecordsNobodysCall()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxBridgedDialTests.AgentLeg(peer: RemoteLeg)))
            .RespondWith(HttpStatusCode.OK, $$$"""{"data":{"call_control_id":"{{{TransferLeg}}}"}}""")
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var history = new List<TelephonyInteraction>();
        var provider = CreateProvider(handler, history);

        // Act
        var result = await provider.TransferAsync(new TransferRequest { CallId = AgentLeg, To = "+17025550199" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var ring = handler.Requests[1];
        Assert.Equal("+17025550199", ReadString(ring.Body, "to"));
        Assert.Equal("voice-profile-1", ReadString(ring.Body, "outbound_voice_profile_id"));
        Assert.DoesNotContain(TelnyxTransferCommands.TransferLegSipHeader, ring.Body, StringComparison.Ordinal);
        Assert.Null(State(ring.Body).TargetUserId);
        Assert.Empty(history);
        Assert.DoesNotContain(handler.Requests, request => request.Path.EndsWith("/actions/transfer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BlindTransferWhoseLegTelnyxRefuses_LeavesTheCallAsItWas()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxBridgedDialTests.AgentLeg(peer: RemoteLeg)))
            .RespondWith(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"10001"}]}""");
        var provider = CreateProvider(handler, []);

        // Act
        var result = await provider.TransferAsync(ExtensionTransfer(TransferMode.Blind), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal([$"GET /v2/calls/{AgentLeg}", "POST /v2/calls"], handler.Requests.Select(Describe));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WarmTransfer_RingsTheAgentsOwnPhoneForAConsult_NamingTheCallAndItsParty(bool toExtension)
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxBridgedDialTests.AgentLeg(peer: RemoteLeg)))
            .RespondWith(HttpStatusCode.OK, $$$"""{"data":{"call_control_id":"{{{ConsultLeg}}}"}}""")
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var provider = CreateProvider(handler, []);
        var request = toExtension
            ? ExtensionTransfer(TransferMode.Warm)
            : new TransferRequest { CallId = AgentLeg, To = "+17025550199", Mode = TransferMode.Warm, Metadata = CallerMetadata() };

        // Act
        var result = await provider.StartAttendedTransferAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(
            [$"GET /v2/calls/{AgentLeg}", "POST /v2/calls", $"PUT /v2/calls/{AgentLeg}/actions/client_state_update"],
            handler.Requests.Select(Describe));

        // The consult rings the phone that asked, like a keypad dial, never through the outbound profile.
        var consult = handler.Requests[1];
        Assert.Equal("sip:gencred1@sip.telnyx.com", ReadString(consult.Body, "to"));
        Assert.False(HasProperty(consult.Body, "outbound_voice_profile_id"));

        var state = State(consult.Body);
        Assert.Equal(TelnyxOutboundBridgeState.AgentLegIntent, state.Intent);
        Assert.Equal(toExtension ? ColleagueEndpoint : "+17025550199", state.Destination);
        Assert.Equal(AgentLeg, state.ConsultOfCallControlId);
        Assert.Equal(RemoteLeg, state.PartyCallControlId);
        Assert.Equal(Number, state.PartyNumber);
        Assert.Equal(toExtension ? "user-2" : null, state.TargetUserId);
        Assert.Null(state.VoicemailRecipientUserId);
        Assert.True(state.IsConsultAgentLeg);

        Assert.Equal(ConsultLeg, State(handler.Requests[2].Body).PendingTransferCallControlId);

        // The consult is a call of the agent's own, shown and recorded as one.
        Assert.Equal(ConsultLeg, result.Call.CallId);
        Assert.Equal(CallState.Connecting, result.Call.State);
        Assert.Equal(AgentLeg, result.Call.Metadata[TelephonyConstants.CallMetadata.ConsultOf]);
        Assert.Equal(ConsultLeg, result.Call.Metadata[TelephonyConstants.CallMetadata.ConsultId]);
        Assert.Equal(toExtension, result.Call.Metadata.ContainsKey(TelephonyConstants.CallMetadata.ExtensionNumber));
    }

    [Fact]
    public async Task WarmTransferFromAPhoneThatCannotBeRungBack_IsRefused_BeforeAnythingIsDialed()
    {
        // Arrange - the caller's only phone predates answering a leg rung back to it.
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, TelnyxBridgedDialTests.CallStatus(TelnyxBridgedDialTests.AgentLeg(peer: RemoteLeg)));
        var provider = CreateProvider(handler, [], [Credential("credential-1", "gencred1", "connection-1", capable: false)]);
        var request = ExtensionTransfer(TransferMode.Warm);

        // Act
        var result = await provider.StartAttendedTransferAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal([$"GET /v2/calls/{AgentLeg}"], handler.Requests.Select(Describe));
    }

    // Bug: a colleague who took over a handed-over call could not transfer it warm -- "Your phone cannot be rung for the
    // consult right now" -- although their phone was registered and able to take the consult. Their page had been open
    // since before the phone started naming its credential on a transfer, so the request named none, and the consult
    // rang only a credential the request named. The consult now rings the caller's own phone whichever way it is found:
    // the credential named, else the one registered from the connection that asked, else their newest registered one.
    [Theory]
    [InlineData(null, "connection-1", "gencred1")]
    [InlineData("a-credential-renewed-since", "connection-1", "gencred1")]
    [InlineData("someone-elses-credential", "connection-1", "gencred1")]
    [InlineData(null, "connection-2", "gencred2")]
    [InlineData(null, "a-connection-that-registered-nothing", "gencred2")]
    [InlineData("credential-1", "connection-2", "gencred1")]
    public async Task WarmTransferOfAHandedOverCall_RingsTheCallersOwnPhone_EvenWhenTheRequestNamesNoLiveCredential(
        string namedCredential,
        string connectionId,
        string expectedSipUser)
    {
        // Arrange - two windows, each registered on a credential of its own; the second registered last.
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, Status(alive: true, HandedOverState()))
            .RespondWith(HttpStatusCode.OK, $$$"""{"data":{"call_control_id":"{{{ConsultLeg}}}"}}""")
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var provider = CreateProvider(handler, [],
        [
            Credential("credential-2", "gencred2", "connection-2", capable: true, registeredMinute: 40),
            Credential("credential-1", "gencred1", "connection-1", capable: true, registeredMinute: 30),
            Credential("credential-3", "gencred3", "connection-3", capable: false, registeredMinute: 50),
        ]);
        var request = ExtensionTransfer(TransferMode.Warm);
        request.Metadata.Remove(TelephonyConstants.RequestMetadata.SoftPhoneCredentialId);
        request.Metadata[TelephonyConstants.RequestMetadata.SoftPhoneConnectionId] = connectionId;

        if (namedCredential is not null)
        {
            request.Metadata[TelephonyConstants.RequestMetadata.SoftPhoneCredentialId] = namedCredential;
        }

        // Act
        var result = await provider.StartAttendedTransferAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal($"sip:{expectedSipUser}@sip.telnyx.com", ReadString(handler.Requests[1].Body, "to"));
        Assert.Equal(AgentLeg, result.Call.Metadata[TelephonyConstants.CallMetadata.ConsultOf]);
    }

    [Theory]
    [MemberData(nameof(ConsultStatuses))]
    public async Task GetConsult_ReportsWhereTheTransferStands(string scenario, bool callAlive, bool legAlive, TelnyxOutboundBridgeState leg, string status, bool callEnded)
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, Status(callAlive, TelnyxBridgedDialTests.AgentLeg(peer: RemoteLeg)))
            .RespondWith(HttpStatusCode.OK, Status(legAlive, leg));
        var provider = CreateProvider(handler, []);

        // Act
        var result = await provider.GetConsultAsync(new ConsultTransferRequest { CallId = AgentLeg, ConsultCallId = ConsultLeg }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, scenario);
        Assert.Equal(status, result.Call.Metadata[TelephonyConstants.CallMetadata.ConsultStatus]);
        Assert.Equal(callEnded, result.Call.Metadata[TelephonyConstants.CallMetadata.ConsultCallEnded]);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    public static TheoryData<string, bool, bool, TelnyxOutboundBridgeState, string, bool> ConsultStatuses()
        => new()
        {
            { "blind, ringing", true, true, TransferLegState(), TelephonyConstants.ConsultStatuses.Ringing, false },
            { "blind, not answered", true, false, TransferLegState(), TelephonyConstants.ConsultStatuses.Cancelled, false },
            { "blind, handed over", false, true, HandedOverState(), TelephonyConstants.ConsultStatuses.Completed, false },
            { "consult, ringing", true, true, ConsultState(answered: false), TelephonyConstants.ConsultStatuses.Ringing, false },
            { "consult, answered", true, true, ConsultState(answered: true), TelephonyConstants.ConsultStatuses.Connected, false },
            { "consult, destination left", true, false, ConsultState(answered: true), TelephonyConstants.ConsultStatuses.Cancelled, false },
            { "consult, caller left", false, true, ConsultState(answered: true), TelephonyConstants.ConsultStatuses.Cancelled, true },
        };

    [Fact]
    public async Task GetConsult_OfALegRungForAnotherCall_IsRefused()
    {
        // Arrange
        var foreign = ConsultState(answered: true);
        foreign.ConsultOfCallControlId = "somebody-elses-call";
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, Status(true, TelnyxBridgedDialTests.AgentLeg(peer: RemoteLeg)))
            .RespondWith(HttpStatusCode.OK, Status(true, foreign));
        var provider = CreateProvider(handler, []);

        // Act
        var result = await provider.GetConsultAsync(new ConsultTransferRequest { CallId = AgentLeg, ConsultCallId = ConsultLeg }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CompletingAConsultWithAColleague_TakesThemOutOfTheConsult_AndBridgesThemToTheCaller()
    {
        // Arrange
        var consult = ConsultState(answered: true);
        consult.PeerCallControlId = ColleagueLeg;
        consult.TargetUserId = "user-2";
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, Status(true, consult))
            .RespondWith(HttpStatusCode.OK, $$$"""{"data":[{"id":"conference-9","name":"consult-{{{ConsultLeg}}}"}]}""")
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var provider = CreateProvider(handler, []);

        // Act
        var result = await provider.CompleteConsultAsync(new ConsultTransferRequest { CallId = AgentLeg, ConsultCallId = ConsultLeg }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(
            [
                $"GET /v2/calls/{ConsultLeg}",
                $"GET /v2/conferences?filter[name]=consult-{ConsultLeg}",
                "POST /v2/conferences/conference-9/actions/leave",
                $"POST /v2/calls/{ColleagueLeg}/actions/bridge",
                $"PUT /v2/calls/{ColleagueLeg}/actions/client_state_update",
                $"PUT /v2/calls/{RemoteLeg}/actions/client_state_update",
                $"POST /v2/calls/{AgentLeg}/actions/hangup",
                $"POST /v2/calls/{ConsultLeg}/actions/hangup",
            ],
            handler.Requests.Select(Describe));

        Assert.Equal(ColleagueLeg, ReadString(handler.Requests[2].Body, "call_control_id"));

        var bridge = handler.Requests[3];
        Assert.Equal(RemoteLeg, ReadString(bridge.Body, "call_control_id"));
        Assert.Equal("self", ReadString(bridge.Body, "park_after_unbridge"));

        // The colleague now carries the call exactly as a keypad dial does, so they can transfer or merge it again.
        var colleague = State(handler.Requests[4].Body);
        Assert.True(colleague.IsBridgedDialAgentLeg);
        Assert.Equal(RemoteLeg, colleague.PeerCallControlId);
        Assert.Equal(ColleagueLeg, State(handler.Requests[5].Body).PeerCallControlId);

        // Both of the agent's legs go, detached, so neither end reaches back for the caller.
        Assert.True(State(handler.Requests[6].Body).Detached);
        Assert.True(State(handler.Requests[7].Body).Detached);
    }

    [Fact]
    public async Task CompletingAConsultWithANumber_BridgesTheTwoOutsideParties_WhoReleaseEachOther()
    {
        // Arrange
        var consult = ConsultState(answered: true);
        consult.PeerCallControlId = "number-leg-1";
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, Status(true, consult))
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var provider = CreateProvider(handler, []);

        // Act
        var result = await provider.CompleteConsultAsync(new ConsultTransferRequest { CallId = AgentLeg, ConsultCallId = ConsultLeg }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(
            [
                $"GET /v2/calls/{ConsultLeg}",
                "POST /v2/calls/number-leg-1/actions/bridge",
                "PUT /v2/calls/number-leg-1/actions/client_state_update",
                $"PUT /v2/calls/{RemoteLeg}/actions/client_state_update",
                $"POST /v2/calls/{AgentLeg}/actions/hangup",
                $"POST /v2/calls/{ConsultLeg}/actions/hangup",
            ],
            handler.Requests.Select(Describe));
        Assert.False(HasProperty(handler.Requests[1].Body, "park_after_unbridge"));
        Assert.Equal(RemoteLeg, State(handler.Requests[2].Body).ReleaseWithCallControlId);
        Assert.Equal("number-leg-1", State(handler.Requests[3].Body).ReleaseWithCallControlId);
    }

    [Fact]
    public async Task CompletingAConsultTheDestinationHasNotAnswered_IsRefused_WithNothingMoved()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().RespondWith(HttpStatusCode.OK, Status(true, ConsultState(answered: false)));
        var provider = CreateProvider(handler, []);

        // Act
        var result = await provider.CompleteConsultAsync(new ConsultTransferRequest { CallId = AgentLeg, ConsultCallId = ConsultLeg }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal([$"GET /v2/calls/{ConsultLeg}"], handler.Requests.Select(Describe));
    }

    [Fact]
    public async Task CancellingAConsult_HangsUpItsLegAsNotAnswered_SoTheCallGoesBackToTheAgent()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, Status(true, ConsultState(answered: true)))
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var provider = CreateProvider(handler, []);

        // Act
        var result = await provider.CancelConsultAsync(new ConsultTransferRequest { CallId = AgentLeg, ConsultCallId = ConsultLeg }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal([$"GET /v2/calls/{ConsultLeg}", $"POST /v2/calls/{ConsultLeg}/actions/hangup"], handler.Requests.Select(Describe));

        var hungUp = State(handler.Requests[1].Body);
        Assert.Null(hungUp.TargetAnswered);
        Assert.NotEqual(true, hungUp.Detached);
        Assert.Equal(AgentLeg, hungUp.ConsultOfCallControlId);
    }

    [Fact]
    public async Task CancellingABlindTransfer_HangsUpTheLegRingingTheDestination()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, Status(true, TransferLegState()))
            .AlwaysRespondWith(HttpStatusCode.OK, Ok);
        var provider = CreateProvider(handler, []);

        // Act
        var result = await provider.CancelConsultAsync(new ConsultTransferRequest { CallId = AgentLeg, ConsultCallId = TransferLeg }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal([$"GET /v2/calls/{TransferLeg}", $"POST /v2/calls/{TransferLeg}/actions/hangup"], handler.Requests.Select(Describe));
    }

    internal static TelnyxOutboundBridgeState TransferLegState()
        => new()
        {
            Intent = TelnyxOutboundBridgeState.TransferLegIntent,
            PeerCallControlId = RemoteLeg,
            TransferOfCallControlId = AgentLeg,
            TargetUserId = "user-2",
            PartyNumber = Number,
        };

    internal static TelnyxOutboundBridgeState ConsultState(bool answered)
        => new()
        {
            Intent = TelnyxOutboundBridgeState.AgentLegIntent,
            Destination = "+17025550199",
            CallerId = CallerId,
            ConsultOfCallControlId = AgentLeg,
            PartyCallControlId = RemoteLeg,
            PartyNumber = Number,
            PeerCallControlId = "number-leg-1",
            TargetAnswered = answered ? true : null,
        };

    private static TelnyxOutboundBridgeState HandedOverState()
        => new()
        {
            Intent = TelnyxOutboundBridgeState.AgentLegIntent,
            PeerCallControlId = RemoteLeg,
            TransferOfCallControlId = AgentLeg,
        };

    private static TransferRequest ExtensionTransfer(TransferMode mode)
        => new()
        {
            CallId = AgentLeg,
            To = "2",
            IsExtension = true,
            TargetUserId = "user-2",
            Mode = mode,
            Metadata = CallerMetadata(),
        };

    private static Dictionary<string, string> CallerMetadata()
        => new()
        {
            [TelephonyConstants.RequestMetadata.SoftPhoneUserId] = "user-1",
            [TelephonyConstants.RequestMetadata.SoftPhoneCredentialId] = "credential-1",
            [TelephonyConstants.RequestMetadata.SoftPhoneUserDisplayName] = "Agent One",
        };

    private static string Status(bool alive, TelnyxOutboundBridgeState state)
        => $$$"""{"data":{"call_control_id":"leg","is_alive":{{{(alive ? "true" : "false")}}},"client_state":"{{{state.ToClientState()}}}"}}""";

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

    private static int ReadInt(string json, string property)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.GetProperty(property).GetInt32();
    }

    private static bool HasProperty(string json, string property)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.TryGetProperty(property, out _);
    }

    private static TelnyxAgentCredential Credential(string credentialId, string sipUsername, string connectionId, bool capable, int registeredMinute = 30)
        => new()
        {
            UserId = "user-1",
            CredentialId = credentialId,
            SipUsername = sipUsername,
            IssuedUtc = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc),
            ExpiresUtc = new DateTime(2026, 1, 1, 14, 0, 0, DateTimeKind.Utc),
            RegisteredUtc = new DateTime(2026, 1, 1, 11, registeredMinute, 0, DateTimeKind.Utc),
            RegisteredConnectionId = connectionId,
            ClientCapabilities = capable ? [TelephonyConstants.SoftPhoneClientCapabilities.BridgedDialLeg] : [],
        };

    private static TelnyxTelephonyProvider CreateProvider(
        RecordingHttpMessageHandler handler,
        List<TelephonyInteraction> history,
        IReadOnlyList<TelnyxAgentCredential> liveCredentials = null)
    {
        var options = new TelnyxOptions
        {
            IsEnabled = true,
            ApiBaseUrl = "https://api.telnyx.com/v2/",
            ApiKey = "test-api-key",
            ConnectionId = "test-connection",
            DefaultOutboundCallerId = CallerId,
            OutboundVoiceProfileId = "voice-profile-1",
        };

        var credentials = new Mock<ITelnyxAgentCredentialStore>();
        credentials.Setup(x => x.ListLiveByUserAsync("user-1", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(liveCredentials ?? [Credential("credential-1", "gencred1", "connection-1", capable: true)]);

        var resolver = new Mock<ITelnyxAgentEndpointResolver>();
        resolver.Setup(x => x.ResolveAsync("user-2", It.IsAny<CancellationToken>())).ReturnsAsync(ColleagueEndpoint);

        var store = new Mock<ITelephonyInteractionStore>();
        store.Setup(x => x.CreateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()))
            .Callback<TelephonyInteraction, CancellationToken>((interaction, _) => history.Add(interaction))
            .Returns(Task.CompletedTask);

        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        return new TelnyxTelephonyProvider(
            new TelnyxApiClient(
                new HttpClient(handler) { BaseAddress = new Uri("https://api.telnyx.com/v2/") },
                new OptionsWrapper<TelnyxOptions>(options),
                new TelnyxApiRetryPolicy(TimeSpan.Zero),
                NullLogger<TelnyxApiClient>.Instance),
            credentials.Object,
            resolver.Object,
            clock.Object,
            NullLogger<TelnyxTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<TelnyxTelephonyProvider>(),
            new TestOptionsMonitor<TelnyxOptions>(options),
            store.Object);
    }
}
