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
/// A number dialed on the soft phone's keypad, connected on the server through the dialing browser's own leg.
/// </summary>
/// <remarks>
/// The browser used to dial these itself, on the credential connection, which reports nothing to the platform: such a
/// call had no call-control id, so it could be neither transferred nor merged. Now the platform rings the dialing
/// browser, dials the number and bridges the two, and every later command goes to the dialed party's leg -- a transfer
/// of the agent's own leg would move the agent, and a conference of the agent's legs would join the agent to
/// themselves.
/// </remarks>
public sealed class TelnyxBridgedDialTests
{
    private const string UserId = "user-1";
    private const string CredentialId = "credential-1";
    private const string Number = "+17025550101";
    private const string CallerId = "+17785550000";
    private const string VoiceProfile = "voice-profile-1";

    [Fact]
    public async Task KeypadDial_RingsTheDialingPhonesOwnCredential_AndCarriesTheNumberToDialOnceItAnswers()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"call_control_id":"agent-leg-1"}}""");
        var provider = CreateProvider(handler, Credentials(Registered(CredentialId, "gencred1")));

        // Act
        var result = await provider.DialAsync(KeypadDial(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("agent-leg-1", result.Call.CallId);
        Assert.Equal(Number, result.Call.To);

        var dial = Assert.Single(handler.Requests);
        Assert.Equal("POST /v2/calls", Describe(dial));
        Assert.Equal("sip:gencred1@sip.telnyx.com", ReadString(dial.Body, "to"));
        Assert.False(HasProperty(dial.Body, "outbound_voice_profile_id"));

        Assert.True(TelnyxOutboundBridgeState.TryParseEncoded(ReadString(dial.Body, "client_state"), out var state));
        Assert.Equal(TelnyxOutboundBridgeState.AgentLegIntent, state.Intent);
        Assert.Equal(Number, state.Destination);
        Assert.Equal(CallerId, state.CallerId);
        Assert.Null(state.VoicemailRecipientUserId);
    }

    // Bug guarded: with the soft phone open in two windows, ringing whichever registered last put the call in the
    // window that did not dial it.
    [Fact]
    public async Task KeypadDial_RingsTheWindowThatDialed_NotTheOneThatRegisteredLast()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"call_control_id":"agent-leg-1"}}""");
        var provider = CreateProvider(handler, Credentials(
            Registered("credential-other", "gencredOther", registeredMinutesAgo: 1),
            Registered(CredentialId, "gencred1", registeredMinutesAgo: 30)));

        // Act
        await provider.DialAsync(KeypadDial(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("sip:gencred1@sip.telnyx.com", ReadString(Assert.Single(handler.Requests).Body, "to"));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("unregistered")]
    [InlineData("old-client")]
    [InlineData("someone-else")]
    public async Task KeypadDial_ThatCannotReachThePhone_DialsNothing_AndTellsThePhoneToDialItself(string scenario)
    {
        // Arrange
        var credential = scenario switch
        {
            "unregistered" => Registered(CredentialId, "gencred1", registered: false),
            "old-client" => Registered(CredentialId, "gencred1", capabilities: []),
            "someone-else" => Registered("credential-of-another-window-that-was-revoked", "gencred9"),
            _ => null,
        };
        var handler = new RecordingHttpMessageHandler();
        var provider = CreateProvider(handler, credential is null ? Credentials() : Credentials(credential));

        // Act
        var result = await provider.DialAsync(KeypadDial(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.OutcomeUnknown);
        Assert.Equal(TelephonyConstants.ErrorCodes.BridgeUnavailable, result.ErrorCode);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task KeypadDial_WhoseAgentLegTelnyxRefuses_TellsThePhoneToDialItself()
    {
        // Arrange - a refusal creates no leg, so the phone dialing instead reaches the number once.
        var handler = new RecordingHttpMessageHandler().RespondWith(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90018"}]}""");
        var provider = CreateProvider(handler, Credentials(Registered(CredentialId, "gencred1")));

        // Act
        var result = await provider.DialAsync(KeypadDial(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelephonyConstants.ErrorCodes.BridgeUnavailable, result.ErrorCode);
    }

    [Fact]
    public async Task KeypadDial_WhoseAgentLegTelnyxDidNotConfirm_IsNotDialedAgainFromTheBrowser()
    {
        // Arrange - a leg that may exist rings this browser, which would then dial the number a second time.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.BadGateway);
        var provider = CreateProvider(handler, Credentials(Registered(CredentialId, "gencred1")));

        // Act
        var result = await provider.DialAsync(KeypadDial(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.OutcomeUnknown);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Telnyx_AdvertisesThatItConnectsKeypadDialsItself()
    {
        var provider = CreateProvider(new RecordingHttpMessageHandler(), Credentials());

        Assert.True(provider.Capabilities.HasFlag(TelephonyCapabilities.BridgedDial));
        Assert.Equal(typeof(ITelephonyCallControlProvider), TelephonyCapabilityContracts.GetContract(TelephonyCapabilities.BridgedDial));
    }

    [Fact]
    public async Task Transfer_MovesTheDialedParty_AndReleasesTheAgentsLeg()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, CallStatus(AgentLeg(peer: "remote-leg-1")))
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = CreateProvider(handler, Credentials());

        // Act
        var result = await provider.TransferAsync(
            new TransferRequest { CallId = "agent-leg-1", To = "+17025550199" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("agent-leg-1", result.Call.CallId);
        Assert.Equal(CallState.Disconnected, result.Call.State);
        Assert.Equal(
            [
                "GET /v2/calls/agent-leg-1",
                "POST /v2/calls/remote-leg-1/actions/transfer",
                "POST /v2/calls/agent-leg-1/actions/hangup",
            ],
            handler.Requests.Select(Describe));

        var transfer = handler.Requests[1];
        Assert.Equal("+17025550199", ReadString(transfer.Body, "to"));
        Assert.Equal(CallerId, ReadString(transfer.Body, "from"));

        // Neither leg's end may reach back for the other one any more.
        Assert.True(TelnyxOutboundBridgeState.TryParseEncoded(ReadString(transfer.Body, "client_state"), out var remoteState));
        Assert.True(remoteState.Detached);
        Assert.True(TelnyxOutboundBridgeState.TryParseEncoded(ReadString(handler.Requests[2].Body, "client_state"), out var agentState));
        Assert.True(agentState.Detached);
    }

    [Fact]
    public async Task TransferToAnExtension_SendsTheDialedPartyToTheColleaguesBrowser_WithoutTheOutboundVoiceProfile()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, CallStatus(AgentLeg(peer: "remote-leg-1")))
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var resolver = new Mock<ITelnyxAgentEndpointResolver>();
        resolver.Setup(x => x.ResolveAsync("user-2", It.IsAny<CancellationToken>())).ReturnsAsync("sip:gencred2@sip.telnyx.com");
        var provider = CreateProvider(handler, Credentials(), resolver.Object);

        // Act
        var result = await provider.TransferAsync(
            new TransferRequest { CallId = "agent-leg-1", To = "2", IsExtension = true, TargetUserId = "user-2" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var transfer = handler.Requests[1];
        Assert.Equal("/v2/calls/remote-leg-1/actions/transfer", transfer.Path);
        Assert.Equal("sip:gencred2@sip.telnyx.com", ReadString(transfer.Body, "to"));
        Assert.False(HasProperty(transfer.Body, "outbound_voice_profile_id"));
    }

    [Fact]
    public async Task WarmTransferOfADialedNumber_IsRefused_BeforeAnythingMoves()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, CallStatus(AgentLeg(peer: "remote-leg-1")));
        var provider = CreateProvider(handler, Credentials());

        // Act
        var result = await provider.StartAttendedTransferAsync(
            new TransferRequest { CallId = "agent-leg-1", To = "+17025550199", Mode = TransferMode.Warm },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(["GET /v2/calls/agent-leg-1"], handler.Requests.Select(Describe));
    }

    [Fact]
    public async Task TransferOfACallThatIsNotADialedNumber_ActsOnTheCallItself()
    {
        // Arrange - an extension call's agent leg carries no dialed party.
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, CallStatus(new TelnyxOutboundBridgeState { Intent = TelnyxOutboundBridgeState.AgentLegIntent, VoicemailRecipientUserId = "user-2", PeerCallControlId = "x" }))
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = CreateProvider(handler, Credentials());

        // Act
        await provider.TransferAsync(new TransferRequest { CallId = "call-1", To = "+17025550199" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["GET /v2/calls/call-1", "POST /v2/calls/call-1/actions/transfer"], handler.Requests.Select(Describe));
    }

    [Fact]
    public async Task MergingThreeDialedNumbers_JoinsTheThreeDialedParties_AndTheAgentOnce()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, CallStatus(AgentLeg(peer: "remote-a")))
            .RespondWith(HttpStatusCode.OK, """{"data":{"id":"conference-1"}}""")
            .RespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""")
            .RespondWith(HttpStatusCode.OK, CallStatus(AgentLeg(peer: "remote-b")))
            .RespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""")
            .RespondWith(HttpStatusCode.OK, CallStatus(AgentLeg(peer: "remote-c")))
            .RespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = CreateProvider(handler, Credentials());

        // Act
        var result = await provider.MergeAsync(new MergeRequest { CallIds = ["agent-a", "agent-b", "agent-c"] }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(
            [
                "GET /v2/calls/agent-a",
                "POST /v2/conferences",
                "POST /v2/conferences/conference-1/actions/join",
                "GET /v2/calls/agent-b",
                "POST /v2/conferences/conference-1/actions/join",
                "GET /v2/calls/agent-c",
                "POST /v2/conferences/conference-1/actions/join",
            ],
            handler.Requests.Select(Describe));

        // The conference is made from the first dialed party, which is detached so its leaving does not end it for
        // everyone else; the agent joins once, on the first leg, and leaving ends the conference.
        var create = handler.Requests[1];
        Assert.Equal("remote-a", ReadString(create.Body, "call_control_id"));
        Assert.Equal("conf-agent-a", ReadString(create.Body, "name"));
        Assert.True(TelnyxOutboundBridgeState.TryParseEncoded(ReadString(create.Body, "client_state"), out var firstParty));
        Assert.True(firstParty.Detached);

        Assert.Equal("agent-a", ReadString(handler.Requests[2].Body, "call_control_id"));
        Assert.True(ReadBoolean(handler.Requests[2].Body, "end_conference_on_exit"));

        Assert.Equal("remote-b", ReadString(handler.Requests[4].Body, "call_control_id"));
        Assert.Equal("remote-c", ReadString(handler.Requests[6].Body, "call_control_id"));

        // No leg of the agent's own is joined twice, and none is hung up: each stays with its participant.
        Assert.DoesNotContain(handler.Requests, request => request.Path.EndsWith("/actions/hangup", StringComparison.Ordinal));
        Assert.Equal("conf-agent-a", result.Call.Metadata["conferenceName"]);
    }

    [Fact]
    public async Task AddingADialedNumberToARunningConference_JoinsOnlyItsDialedParty()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, """{"data":[{"id":"conference-1","name":"conf-agent-a"}]}""")
            .RespondWith(HttpStatusCode.OK, CallStatus(AgentLeg(peer: "remote-d")))
            .RespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = CreateProvider(handler, Credentials());

        // Act
        var result = await provider.MergeAsync(
            new MergeRequest { CallIds = ["agent-a", "agent-d"], ConferenceName = "conf-agent-a" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(
            [
                "GET /v2/conferences?filter[name]=conf-agent-a",
                "GET /v2/calls/agent-d",
                "POST /v2/conferences/conference-1/actions/join",
            ],
            handler.Requests.Select(Describe));
        Assert.Equal("remote-d", ReadString(handler.Requests[2].Body, "call_control_id"));
    }

    [Fact]
    public async Task DigitsOnADialedNumber_ArePlayedFromTheDialedPartysLeg()
    {
        // Arrange - played from the agent's own leg, they would be heard in the agent's own browser.
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, CallStatus(AgentLeg(peer: "remote-leg-1")))
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = CreateProvider(handler, Credentials());

        // Act
        var result = await provider.SendDigitsAsync(new SendDigitsRequest { CallId = "agent-leg-1", Digits = "12#" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("POST /v2/calls/remote-leg-1/actions/send_dtmf", Describe(handler.Requests[1]));
        Assert.Equal("12#", ReadString(handler.Requests[1].Body, "digits"));
    }

    internal static TelnyxOutboundBridgeState AgentLeg(string peer)
        => new()
        {
            Intent = TelnyxOutboundBridgeState.AgentLegIntent,
            Destination = Number,
            CallerId = CallerId,
            PeerCallControlId = peer,
        };

    internal static string CallStatus(TelnyxOutboundBridgeState state)
        => $$$"""{"data":{"call_control_id":"leg","is_alive":true,"client_state":"{{{state.ToClientState()}}}"}}""";

    private static DialRequest KeypadDial()
        => new()
        {
            To = Number,
            Metadata = new Dictionary<string, string>
            {
                [TelephonyConstants.RequestMetadata.SoftPhoneUserId] = UserId,
                [TelephonyConstants.RequestMetadata.SoftPhoneCredentialId] = CredentialId,
            },
        };

    private static TelnyxAgentCredential Registered(
        string credentialId,
        string sipUsername,
        bool registered = true,
        int registeredMinutesAgo = 5,
        string[] capabilities = null)
        => new()
        {
            UserId = UserId,
            CredentialId = credentialId,
            SipUsername = sipUsername,
            IssuedUtc = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc),
            ExpiresUtc = new DateTime(2026, 1, 1, 14, 0, 0, DateTimeKind.Utc),
            RegisteredUtc = registered ? new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddMinutes(-registeredMinutesAgo) : null,
            ClientCapabilities = capabilities ?? [TelephonyConstants.SoftPhoneClientCapabilities.BridgedDialLeg],
        };

    private static ITelnyxAgentCredentialStore Credentials(params TelnyxAgentCredential[] live)
    {
        var store = new Mock<ITelnyxAgentCredentialStore>();

        store.Setup(x => x.ListLiveByUserAsync(UserId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(live);

        return store.Object;
    }

    private static string Describe(RecordingHttpMessageHandler.RecordedRequest request)
        => $"{request.Method} {Uri.UnescapeDataString(request.Path)}";

    private static string ReadString(string json, string property)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.GetProperty(property).GetString();
    }

    private static bool ReadBoolean(string json, string property)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
    }

    private static bool HasProperty(string json, string property)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.TryGetProperty(property, out _);
    }

    private static TelnyxTelephonyProvider CreateProvider(
        RecordingHttpMessageHandler handler,
        ITelnyxAgentCredentialStore credentialStore,
        ITelnyxAgentEndpointResolver endpointResolver = null)
    {
        var options = new TelnyxOptions
        {
            IsEnabled = true,
            ApiBaseUrl = "https://api.telnyx.com/v2/",
            ApiKey = "test-api-key",
            ConnectionId = "test-connection",
            DefaultOutboundCallerId = CallerId,
            OutboundVoiceProfileId = VoiceProfile,
        };

        var apiClient = new TelnyxApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.telnyx.com/v2/") },
            new OptionsWrapper<TelnyxOptions>(options),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);

        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        return new TelnyxTelephonyProvider(
            apiClient,
            credentialStore,
            endpointResolver ?? new Mock<ITelnyxAgentEndpointResolver>().Object,
            clock.Object,
            NullLogger<TelnyxTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<TelnyxTelephonyProvider>(),
            new TestOptionsMonitor<TelnyxOptions>(options));
    }
}
