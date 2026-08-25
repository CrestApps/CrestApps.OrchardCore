using System.Net;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Covers the Telnyx no-answer → voicemail routing for internal extension calls, and the bridge-state fields
/// that carry the voicemail recipient and ring timeout between legs.
/// </summary>
public sealed class TelnyxExtensionVoicemailTests
{
    [Fact]
    public void BridgeState_RoundTrips_VoicemailRecipientAndRingTimeout()
    {
        var state = new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.AgentLegIntent,
            Destination = "sip:bob@example.com",
            VoicemailRecipientUserId = "user-1",
            RingTimeoutSeconds = 25,
        };

        Assert.True(TelnyxOutboundBridgeState.TryParse(DecodeClientState(state.ToClientState()), out var parsed));
        Assert.Equal("user-1", parsed.VoicemailRecipientUserId);
        Assert.Equal(25, parsed.RingTimeoutSeconds);
    }

    [Fact]
    public async Task NoAnswerHangup_WithVoicemailRecipient_StartsVoicemailRecordingOnCallerLeg()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{}}");
        var orchestrator = CreateOrchestrator(handler);

        await orchestrator.AdvanceAsync(
            DestinationHangup(hangupCause: "NO_ANSWER", recipientUserId: "user-1", agentLegId: "agent-1"),
            TestContext.Current.CancellationToken);

        Assert.Contains(handler.Requests, r =>
            r.RequestUri.AbsolutePath.EndsWith("calls/agent-1/actions/record_start", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NormalClearingHangup_DoesNotRouteToVoicemail()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{}}");
        var orchestrator = CreateOrchestrator(handler);

        await orchestrator.AdvanceAsync(
            DestinationHangup(hangupCause: "NORMAL_CLEARING", recipientUserId: "user-1", agentLegId: "agent-1"),
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(handler.Requests, r =>
            r.RequestUri.AbsolutePath.EndsWith("record_start", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoAnswerHangup_WithoutVoicemailRecipient_DoesNotRecord()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{}}");
        var orchestrator = CreateOrchestrator(handler);

        await orchestrator.AdvanceAsync(
            DestinationHangup(hangupCause: "NO_ANSWER", recipientUserId: null, agentLegId: "agent-1"),
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(handler.Requests, r =>
            r.RequestUri.AbsolutePath.EndsWith("record_start", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AgentAnswered_InternalSipDestination_OmitsOutboundVoiceProfile()
    {
        // When the agent leg answers, the orchestrator dials the destination. An extension call targets another
        // registered browser credential (a sip: URI); carrying the outbound voice profile would make Telnyx route
        // it as an outbound/PSTN call and never deliver it to the credential, so it must be omitted.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{\"call_control_id\":\"dest-1\"}}");
        var orchestrator = CreateOrchestrator(handler, outboundVoiceProfileId: "vp-1");

        await orchestrator.AdvanceAsync(
            AgentAnswered(destination: "sip:agent2@sip.telnyx.com"),
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain("outbound_voice_profile_id", handler.LastRequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AgentAnswered_PstnDestination_IncludesOutboundVoiceProfile()
    {
        // A PSTN destination (a real outbound bridge) is still terminated through the outbound voice profile.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{\"call_control_id\":\"dest-1\"}}");
        var orchestrator = CreateOrchestrator(handler, outboundVoiceProfileId: "vp-1");

        await orchestrator.AdvanceAsync(
            AgentAnswered(destination: "+15551234567"),
            TestContext.Current.CancellationToken);

        Assert.Contains("outbound_voice_profile_id", handler.LastRequestBody, StringComparison.Ordinal);
    }

    private static TelnyxCallEvent AgentAnswered(string destination)
    {
        var clientState = DecodeClientState(new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.AgentLegIntent,
            Destination = destination,
            CallerId = "+17787204596",
            VoicemailRecipientUserId = "user-1",
            RingTimeoutSeconds = 25,
        }.ToClientState());

        return new TelnyxCallEvent
        {
            EventType = "call.answered",
            CallControlId = "agent-1",
            ClientState = clientState,
        };
    }

    private static TelnyxCallEvent DestinationHangup(string hangupCause, string recipientUserId, string agentLegId)
    {
        // The webhook parser base64-decodes client_state before the orchestrator sees it, so the event carries
        // decoded JSON.
        var clientState = DecodeClientState(new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
            PeerCallControlId = agentLegId,
            VoicemailRecipientUserId = recipientUserId,
        }.ToClientState());

        return new TelnyxCallEvent
        {
            EventType = "call.hangup",
            CallControlId = "dest-1",
            HangupCause = hangupCause,
            ClientState = clientState,
        };
    }

    private static TelnyxOutboundBridgeOrchestrator CreateOrchestrator(StubHttpMessageHandler handler, string outboundVoiceProfileId = null)
        => new(
            new StubHttpClientFactory(handler),
            NullLogger<TelnyxOutboundBridgeOrchestrator>.Instance,
            new TestOptionsMonitor<TelnyxOptions>(new TelnyxOptions
            {
                IsEnabled = true,
                ApiKey = "KEY",
                ConnectionId = "connection-1",
                ApiBaseUrl = "https://api.telnyx.test/v2/",
                OutboundVoiceProfileId = outboundVoiceProfileId,
            }));

    private static string DecodeClientState(string clientState)
        => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(clientState));
}
