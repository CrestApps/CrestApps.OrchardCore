using System.Net.Http.Headers;
using System.Net.Http.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Default implementation of <see cref="ITelnyxOutboundBridgeOrchestrator"/>. It reacts to the
/// <c>call.answered</c> events of the two legs the platform created for a browser-audio outbound call and
/// issues the follow-up Telnyx Call Control commands over REST. All correlation travels in the leg's
/// <c>client_state</c>, so no server-side call registry is required.
/// </summary>
public sealed class TelnyxOutboundBridgeOrchestrator : ITelnyxOutboundBridgeOrchestrator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TelnyxOutboundBridgeOrchestrator> _logger;
    private readonly IContactCenterAgentLegFailureService _agentLegFailureService;
    private readonly IEnumerable<ITelnyxAiVoiceEventHandler> _aiVoiceEventHandlers;
    private readonly TelnyxOptions _options;

    public TelnyxOutboundBridgeOrchestrator(
        IHttpClientFactory httpClientFactory,
        ILogger<TelnyxOutboundBridgeOrchestrator> logger,
        IOptionsMonitor<TelnyxOptions> telnyxOptions,
        IContactCenterAgentLegFailureService agentLegFailureService,
        IEnumerable<ITelnyxAiVoiceEventHandler> aiVoiceEventHandlers)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = telnyxOptions.CurrentValue;
        _agentLegFailureService = agentLegFailureService;
        _aiVoiceEventHandlers = aiVoiceEventHandlers;
    }

    /// <inheritdoc/>
    public async Task<TelnyxOutboundBridgeLeg> AdvanceAsync(TelnyxCallEvent callEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callEvent);

        if (!TelnyxOutboundBridgeState.TryParse(callEvent.ClientState, out var state))
        {
            return TelnyxOutboundBridgeLeg.None;
        }

        var isAnswered = string.Equals(callEvent.EventType?.Trim(), "call.answered", StringComparison.OrdinalIgnoreCase);

        if (state.Intent == TelnyxOutboundBridgeState.AiVoiceLegIntent)
        {
            // A leg an automated AI voice agent handles. Its lifecycle (answered, transcription, speak-ended,
            // hangup) drives the conversation loop in the optional AI voice handler. The leg is never a human
            // agent's call, so it is reported as a hidden internal leg (DestinationLeg) to keep its events out of
            // Contact Center normalization -- otherwise the webhook pipeline would try to reserve an agent for it.
            foreach (var handler in _aiVoiceEventHandlers)
            {
                await handler.HandleAsync(callEvent, state, cancellationToken);
            }

            return TelnyxOutboundBridgeLeg.DestinationLeg;
        }

        if (state.Intent == TelnyxOutboundBridgeState.AgentLegIntent)
        {
            // The agent's browser answered the leg we rang; dial the destination it wanted to reach.
            if (isAnswered && _options.IsConfigured && !string.IsNullOrWhiteSpace(state.Destination))
            {
                await DialDestinationAsync(agentLegCallControlId: callEvent.CallControlId, state, cancellationToken);
            }

            return TelnyxOutboundBridgeLeg.AgentLeg;
        }

        if (state.Intent == TelnyxOutboundBridgeState.ContactCenterAgentLegIntent)
        {
            // The Contact Center agent's browser answered; bridge it to the already-answered caller leg. The
            // agent leg is a tracked leg of the interaction, so let its events flow to normalization (return
            // None) rather than treating it as an internal leg to hide.
            if (isAnswered && !string.IsNullOrWhiteSpace(state.PeerCallControlId))
            {
                if (_options.IsConfigured)
                {
                    await BridgeAsync(destinationLegCallControlId: callEvent.CallControlId, agentLegCallControlId: state.PeerCallControlId, cancellationToken);
                }

                // The agent leg's own call.answered is keyed by the agent-leg call id, which belongs to no
                // interaction, so normalization discards it and the agent leg already recorded on the call
                // topology never advances past dialing -- and is later marked failed with no answered time,
                // misreporting who was on the call and its talk time. Record the answer against the customer call
                // the leg was joining (carried in client_state) so the topology reflects that the agent connected.
                await _agentLegFailureService.RecordAnsweredAsync(
                    TelnyxConstants.ProviderTechnicalName,
                    peerProviderCallId: state.PeerCallControlId,
                    agentLegProviderCallId: callEvent.CallControlId,
                    cancellationToken);
            }
            else if (IsHangup(callEvent))
            {
                // Report what the provider actually said about the leg. The normalized cause collapses several
                // provider outcomes onto one value, and the SIP response is what separates them: a rejection by
                // the endpoint, a rejection by the platform's own routing policy, and a busy endpoint all arrive
                // as a refusal but mean different things and are fixed in different places. Logged for every
                // terminal agent leg, not only the ones treated as a connect failure, so a cause this code does
                // not yet recognize is still named rather than silently ignored.
                _logger.LogWarning(
                    "A Contact Center agent leg ended without answering. HangupCause={HangupCause}, SipHangupCause={SipHangupCause}, HangupSource={HangupSource}, To={ToAddress}, PeerCallControlId={PeerCallControlId}, TreatedAsConnectFailure={TreatedAsConnectFailure}.",
                    callEvent.HangupCause.SanitizeLogValue(),
                    callEvent.SipHangupCause.SanitizeLogValue(),
                    callEvent.HangupSource.SanitizeLogValue(),
                    callEvent.To.SanitizeLogValue(),
                    state.PeerCallControlId.SanitizeLogValue(),
                    IsAgentLegConnectFailure(callEvent));
            }

            if (!isAnswered && IsAgentLegConnectFailure(callEvent) && !string.IsNullOrWhiteSpace(state.PeerCallControlId))
            {
                // The agent leg died before it was ever answered -- rejected by the endpoint, unanswered, or
                // cleared without ringing. Its identifier belongs to no interaction, so normalization discards
                // it and nothing else ever learns the connect failed: the customer is left on a call with an
                // agent who was never reached, and the agent is left holding work they cannot finish. The peer
                // identifier in the leg's own client_state is the call that failed, so report it here.
                await _agentLegFailureService.FailAsync(
                    TelnyxConstants.ProviderTechnicalName,
                    state.PeerCallControlId,
                    ResolveAgentLegFailureCause(callEvent),
                    cancellationToken);
            }

            return TelnyxOutboundBridgeLeg.None;
        }

        if (state.Intent == TelnyxOutboundBridgeState.ConferenceExtensionLegIntent)
        {
            // An internal extension participant answered; join it to the conference formed from the active call.
            // It is a tracked participant leg, so let its events flow to normalization (return None).
            if (isAnswered && _options.IsConfigured && !string.IsNullOrWhiteSpace(state.PeerCallControlId))
            {
                await JoinConferenceAsync(answeredLegCallControlId: callEvent.CallControlId, state, cancellationToken);
            }

            return TelnyxOutboundBridgeLeg.None;
        }

        // The destination answered; connect it to the agent leg that has been waiting.
        if (isAnswered && _options.IsConfigured && !string.IsNullOrWhiteSpace(state.PeerCallControlId))
        {
            // An internal extension call connects two WebRTC (telnyx-rtc) browser legs. Telnyx's raw two-leg
            // bridge does not reliably pass media between two WebRTC legs -- the SDP negotiates cleanly and DTLS
            // comes up, but audio flows only one way (one leg receives nothing). Joining the two legs through a
            // conference (Telnyx's media mixer) fixes it, because each WebRTC leg negotiates normal two-way media
            // with the mixer just like it does on a working PSTN call. A regular PSTN destination keeps the direct
            // bridge. An internal extension call is the one that carries a voicemail recipient.
            var isInternalExtensionCall = !string.IsNullOrWhiteSpace(state.VoicemailRecipientUserId);

            if (isInternalExtensionCall)
            {
                await ConnectExtensionViaConferenceAsync(
                    agentLegCallControlId: state.PeerCallControlId,
                    destinationLegCallControlId: callEvent.CallControlId,
                    cancellationToken);
            }
            else
            {
                await BridgeAsync(destinationLegCallControlId: callEvent.CallControlId, agentLegCallControlId: state.PeerCallControlId, cancellationToken);
            }

            return TelnyxOutboundBridgeLeg.DestinationLeg;
        }

        // The destination hung up without answering. For an internal extension call that names a voicemail
        // recipient, route the still-connected caller (agent) leg to that user's voicemail instead of just
        // ending the call. A caller-canceled or normal-clearing hangup is not a no-answer, so it is left alone.
        var isHangup = string.Equals(callEvent.EventType?.Trim(), "call.hangup", StringComparison.OrdinalIgnoreCase);

        if (isHangup && _options.IsConfigured && !string.IsNullOrWhiteSpace(state.PeerCallControlId))
        {
            var isInternalExtensionCall = !string.IsNullOrWhiteSpace(state.VoicemailRecipientUserId);

            if (IsNoAnswerHangup(callEvent) && isInternalExtensionCall)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Extension bridge: destination leg {CallControlId} did not answer (cause {HangupCause}); routing caller leg {AgentLeg} to voicemail for user {RecipientUserId}.",
                        callEvent.CallControlId.SanitizeLogValue(),
                        callEvent.HangupCause.SanitizeLogValue(),
                        state.PeerCallControlId.SanitizeLogValue(),
                        state.VoicemailRecipientUserId.SanitizeLogValue());
                }

                await RouteToVoicemailAsync(
                    agentLegCallControlId: state.PeerCallControlId,
                    recipientUserId: state.VoicemailRecipientUserId,
                    cancellationToken);
            }
            else if (isInternalExtensionCall)
            {
                // The callee's leg of an internal extension call ended after it was connected (the callee hung
                // up). The two legs are joined through a conference, so ending one participant does not end the
                // other; hang up the caller's (agent) leg too so the call clears for both. Idempotent: if the
                // caller hung up first (which ended the conference and this leg), the agent leg is already gone.
                await HangupLegAsync(state.PeerCallControlId, cancellationToken);
            }
        }

        return TelnyxOutboundBridgeLeg.DestinationLeg;
    }

    // Telnyx hangup causes that mean an agent leg the platform originated never reached the agent. It is a
    // superset of the no-answer causes: an endpoint that is not registered, or that declines, refuses the invite
    // outright rather than letting it ring out. NORMAL_CLEARING is deliberately absent -- that is the agent leg
    // of a real conversation ending, which is not a connect failure.
    private static readonly HashSet<string> _agentLegConnectFailureCauses = new(StringComparer.OrdinalIgnoreCase)
    {
        "TIMEOUT",
        "NO_ANSWER",
        "USER_BUSY",
        "CALL_REJECTED",
        "REJECTED",
        "NORMAL_TEMPORARY_FAILURE",
        "UNALLOCATED_NUMBER",
        "INCOMPATIBLE_DESTINATION",
    };

    private static bool IsHangup(TelnyxCallEvent callEvent)
        => string.Equals(callEvent.EventType?.Trim(), "call.hangup", StringComparison.OrdinalIgnoreCase);

    // SIP responses that mean an agent leg the platform originated was never answered: the endpoint timed out,
    // was unreachable, was busy, was unavailable, or declined the invite. These are read straight from the SIP
    // layer because the provider does not always normalize them faithfully -- an unreachable endpoint (for
    // example a lapsed registration answering 480) can arrive as NORMAL_CLEARING, which reads as an ordinary end
    // and would leave the customer on a call with an agent who was never reached. A leg that carried a real
    // conversation clears with 200, so it is never in this set.
    private static readonly HashSet<string> _agentLegConnectFailureSipCauses = new(StringComparer.OrdinalIgnoreCase)
    {
        "408",
        "480",
        "486",
        "503",
        "603",
    };

    private static bool IsAgentLegConnectFailure(TelnyxCallEvent callEvent)
    {
        if (!IsHangup(callEvent))
        {
            return false;
        }

        var cause = callEvent.HangupCause?.Trim();

        if (!string.IsNullOrEmpty(cause) && _agentLegConnectFailureCauses.Contains(cause))
        {
            return true;
        }

        var sipCause = callEvent.SipHangupCause?.Trim();

        return !string.IsNullOrEmpty(sipCause) && _agentLegConnectFailureSipCauses.Contains(sipCause);
    }

    // The recorded failure cause prefers the provider's normalized cause, but falls back to the SIP response when
    // the provider normalized an unreachable-endpoint response to a non-failure cause, so a released call is not
    // recorded as having ended normally.
    private static HangupCause? ResolveAgentLegFailureCause(TelnyxCallEvent callEvent)
    {
        var mapped = MapHangupCause(callEvent.HangupCause);

        if (mapped is null or HangupCause.NormalClearing)
        {
            var sipMapped = MapSipHangupCause(callEvent.SipHangupCause);

            if (sipMapped.HasValue)
            {
                return sipMapped;
            }
        }

        return mapped;
    }

    private static HangupCause? MapSipHangupCause(string sipHangupCause)
        => sipHangupCause?.Trim() switch
        {
            "408" or "480" => HangupCause.NoAnswer,
            "486" => HangupCause.Busy,
            "603" => HangupCause.Rejected,
            "503" => HangupCause.Failed,
            _ => null,
        };

    private static HangupCause? MapHangupCause(string hangupCause)
        => hangupCause?.Trim().ToUpperInvariant() switch
        {
            null or "" => null,
            "NORMAL_CLEARING" => Telephony.Models.HangupCause.NormalClearing,
            "TIMEOUT" or "NO_ANSWER" => Telephony.Models.HangupCause.NoAnswer,
            "USER_BUSY" => Telephony.Models.HangupCause.Busy,
            "CALL_REJECTED" or "REJECTED" => Telephony.Models.HangupCause.Rejected,
            "ORIGINATOR_CANCEL" or "CANCELED" or "CANCELLED" => Telephony.Models.HangupCause.Canceled,
            _ => Telephony.Models.HangupCause.Failed,
        };

    // Telnyx hangup causes that mean the target never answered (as opposed to a normal end after a conversation
    // or the caller canceling before answer). Only these route the caller to voicemail.
    private static readonly HashSet<string> _noAnswerHangupCauses = new(StringComparer.OrdinalIgnoreCase)
    {
        "TIMEOUT",
        "NO_ANSWER",
        "USER_BUSY",
        "CALL_REJECTED",
        "NORMAL_TEMPORARY_FAILURE",
    };

    private static bool IsNoAnswerHangup(TelnyxCallEvent callEvent)
    {
        if (!string.Equals(callEvent.EventType?.Trim(), "call.hangup", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(callEvent.HangupCause) &&
            _noAnswerHangupCauses.Contains(callEvent.HangupCause.Trim());
    }

    private async Task RouteToVoicemailAsync(string agentLegCallControlId, string recipientUserId, CancellationToken cancellationToken)
    {
        // The caller's leg is still up (the destination never answered, so it was never bridged). Start a
        // beep-and-record on that leg tagged as the recipient's voicemail, so the existing saved-recording
        // pipeline ingests it into that user's voicemail inbox. The greeting is intentionally not spoken here so
        // this path stays independent of the voicemail-greeting media model; a leading beep tells the caller to
        // record.
        var body = new Dictionary<string, object>
        {
            ["client_state"] = TelnyxRecordingClientState.ForVoicemail(agentLegCallControlId, recipientUserId).ToClientState(),
            ["format"] = TelnyxConstants.Recording.Format,
            ["channels"] = "single",
            ["play_beep"] = true,
            ["command_id"] = $"ext-vm-{agentLegCallControlId}",
        };

        try
        {
            using var client = CreateClient();
            using var content = JsonContent.Create(body, options: TelnyxJsonSerializerOptions.Default);
            using var response = await client.PostAsync(
                $"calls/{Uri.EscapeDataString(agentLegCallControlId)}/actions/record_start",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Telnyx rejected starting extension voicemail recording with status code {StatusCode}. Response: {Response}",
                    response.StatusCode,
                    (await SafeReadContentAsync(response, cancellationToken)).SanitizeLogValue());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while routing an unanswered extension call to voicemail.");
        }
    }

    private async Task JoinConferenceAsync(string answeredLegCallControlId, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        var conferenceName = string.IsNullOrWhiteSpace(state.ConferenceName)
            ? $"conf-{state.PeerCallControlId}"
            : state.ConferenceName;

        try
        {
            using var client = CreateClient();

            // Ensure the conference exists, formed from the active call. The first extension add creates it; a
            // later add finds the existing one. command_id makes a redelivered create idempotent.
            var conferenceId = await EnsureConferenceAsync(client, conferenceName, state.PeerCallControlId, cancellationToken);

            if (string.IsNullOrWhiteSpace(conferenceId))
            {
                _logger.LogError("Could not resolve the Telnyx conference '{ConferenceName}' to add an extension participant.", conferenceName.SanitizeLogValue());

                return;
            }

            using var joinContent = JsonContent.Create(
                new Dictionary<string, object>
                {
                    ["call_control_id"] = answeredLegCallControlId,
                    ["command_id"] = $"ext-conf-join-{answeredLegCallControlId}",
                },
                options: TelnyxJsonSerializerOptions.Default);
            using var joinResponse = await client.PostAsync(
                $"conferences/{Uri.EscapeDataString(conferenceId)}/actions/join",
                joinContent,
                cancellationToken);

            if (!joinResponse.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Telnyx rejected joining an extension participant to conference '{ConferenceName}' with status code {StatusCode}. Response: {Response}",
                    conferenceName.SanitizeLogValue(),
                    joinResponse.StatusCode,
                    (await SafeReadContentAsync(joinResponse, cancellationToken)).SanitizeLogValue());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while joining an extension participant to a Telnyx conference.");
        }
    }

    private static async Task<string> EnsureConferenceAsync(HttpClient client, string conferenceName, string activeCallControlId, CancellationToken cancellationToken)
    {
        using var createContent = JsonContent.Create(
            new Dictionary<string, object>
            {
                ["name"] = conferenceName,
                ["call_control_id"] = activeCallControlId,
                ["command_id"] = $"ext-conf-create-{activeCallControlId}",
            },
            options: TelnyxJsonSerializerOptions.Default);
        using var createResponse = await client.PostAsync("conferences", createContent, cancellationToken);

        if (createResponse.IsSuccessStatusCode)
        {
            return await ReadConferenceIdAsync(createResponse, cancellationToken);
        }

        // The conference already exists (a prior extension add created it); look it up by its deterministic name.
        using var listResponse = await client.GetAsync(
            $"conferences?filter[name]={Uri.EscapeDataString(conferenceName)}",
            cancellationToken);

        if (!listResponse.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await listResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.TryGetProperty("data", out var data) &&
            data.ValueKind == System.Text.Json.JsonValueKind.Array &&
            data.GetArrayLength() > 0 &&
            data[0].TryGetProperty("id", out var idElement))
        {
            return idElement.GetString();
        }

        return null;
    }

    private static async Task<string> ReadConferenceIdAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.TryGetProperty("data", out var data) &&
            data.ValueKind == System.Text.Json.JsonValueKind.Object &&
            data.TryGetProperty("id", out var idElement))
        {
            return idElement.GetString();
        }

        return null;
    }

    private async Task DialDestinationAsync(string agentLegCallControlId, TelnyxOutboundBridgeState agentState, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object>
        {
            ["connection_id"] = _options.ConnectionId,
            ["to"] = agentState.Destination,
            // Correlate the destination leg back to the agent leg so its call.answered can bridge the two, and
            // carry the voicemail recipient so a no-answer destination hangup can send the caller to voicemail.
            ["client_state"] = new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
                PeerCallControlId = agentLegCallControlId,
                VoicemailRecipientUserId = agentState.VoicemailRecipientUserId,
            }.ToClientState(),
            // Telnyx de-duplicates by command_id, so a redelivered agent-answered webhook cannot place a
            // second destination call.
            ["command_id"] = $"ob-dest-{agentLegCallControlId}",
        };

        if (!string.IsNullOrWhiteSpace(agentState.CallerId))
        {
            body["from"] = agentState.CallerId;
        }

        // Present the caller's name to the destination so a callee ringing on an internal extension call sees who
        // is calling instead of just the caller-id number.
        if (!string.IsNullOrWhiteSpace(agentState.CallerDisplayName))
        {
            body["from_display_name"] = agentState.CallerDisplayName;
        }

        // A PSTN destination is terminated through the outbound voice profile, but an internal SIP destination
        // -- an extension call to another registered browser credential (sip:{cred}@...) -- must NOT carry one:
        // with an outbound voice profile Telnyx routes the leg as an outbound/PSTN call and never delivers it to
        // the registered credential, so it clears immediately without ringing. The agent leg reaches the caller's
        // own credential the same way (no voice profile), which is why it connects and this one did not.
        var destinationIsInternalSip = agentState.Destination is not null &&
            agentState.Destination.StartsWith("sip:", StringComparison.OrdinalIgnoreCase);

        if (!destinationIsInternalSip && !string.IsNullOrWhiteSpace(_options.OutboundVoiceProfileId))
        {
            body["outbound_voice_profile_id"] = _options.OutboundVoiceProfileId;
        }

        // Bound the ring so an unanswered internal extension call is released and can fall to voicemail rather
        // than ringing indefinitely.
        if (agentState.RingTimeoutSeconds is > 0)
        {
            body["timeout_secs"] = agentState.RingTimeoutSeconds.Value;
        }

        try
        {
            using var client = CreateClient();
            using var content = JsonContent.Create(body, options: TelnyxJsonSerializerOptions.Default);
            using var response = await client.PostAsync("calls", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Telnyx rejected the destination leg of an outbound bridge with status code {StatusCode}. Response: {Response}",
                    response.StatusCode,
                    (await SafeReadContentAsync(response, cancellationToken)).SanitizeLogValue());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while dialing the destination leg of a Telnyx outbound bridge.");
        }
    }

    // Connect the caller's (agent) leg and the answered destination leg of an internal extension call by placing
    // both into a conference, instead of a raw two-leg bridge. Two WebRTC legs bridged directly negotiate media
    // but only pass audio one way on Telnyx; a conference mixes them and each WebRTC leg gets normal two-way
    // media with the mixer. The conference is named after the agent leg so both legs resolve the same one.
    private async Task ConnectExtensionViaConferenceAsync(
        string agentLegCallControlId,
        string destinationLegCallControlId,
        CancellationToken cancellationToken)
    {
        var conferenceName = $"ext-{agentLegCallControlId}";

        try
        {
            using var client = CreateClient();

            // Form the conference from the destination (callee) leg, then join the caller's (agent) leg with
            // end_conference_on_exit so that when the caller hangs up, Telnyx ends the conference and drops the
            // callee too. (A conference, unlike a raw bridge, otherwise leaves the remaining participant connected
            // when the other hangs up.) The reverse direction -- the callee hanging up first -- is handled by the
            // destination-leg hangup path, which hangs up the caller's leg.
            var conferenceId = await EnsureConferenceAsync(client, conferenceName, destinationLegCallControlId, cancellationToken);

            if (string.IsNullOrWhiteSpace(conferenceId))
            {
                _logger.LogError(
                    "Could not resolve the Telnyx conference '{ConferenceName}' to connect an internal extension call.",
                    conferenceName.SanitizeLogValue());

                return;
            }

            using var joinContent = JsonContent.Create(
                new Dictionary<string, object>
                {
                    ["call_control_id"] = agentLegCallControlId,
                    ["end_conference_on_exit"] = true,
                    ["command_id"] = $"ext-join-{agentLegCallControlId}",
                },
                options: TelnyxJsonSerializerOptions.Default);
            using var joinResponse = await client.PostAsync(
                $"conferences/{Uri.EscapeDataString(conferenceId)}/actions/join",
                joinContent,
                cancellationToken);

            if (!joinResponse.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Telnyx rejected joining the caller leg to conference '{ConferenceName}' with status code {StatusCode}. Response: {Response}",
                    conferenceName.SanitizeLogValue(),
                    joinResponse.StatusCode,
                    (await SafeReadContentAsync(joinResponse, cancellationToken)).SanitizeLogValue());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while connecting an internal extension call through a Telnyx conference.");
        }
    }

    private async Task HangupLegAsync(string callControlId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(callControlId))
        {
            return;
        }

        try
        {
            using var client = CreateClient();
            using var response = await client.PostAsync(
                $"calls/{Uri.EscapeDataString(callControlId)}/actions/hangup",
                content: null,
                cancellationToken);

            // A leg that has already ended returns an error; that is expected (for example the caller hung up
            // first, which ended the conference and this leg), so it is not logged as a failure.
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occurred while hanging up the peer leg {CallControlId} of an internal extension call.", callControlId.SanitizeLogValue());
        }
    }

    private async Task BridgeAsync(string destinationLegCallControlId, string agentLegCallControlId, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object>
        {
            ["call_control_id"] = agentLegCallControlId,
            ["command_id"] = $"ob-bridge-{destinationLegCallControlId}",
        };

        try
        {
            using var client = CreateClient();
            using var content = JsonContent.Create(body, options: TelnyxJsonSerializerOptions.Default);
            using var response = await client.PostAsync(
                $"calls/{Uri.EscapeDataString(destinationLegCallControlId)}/actions/bridge",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Telnyx rejected the bridge of an outbound soft-phone call with status code {StatusCode}. Response: {Response}",
                    response.StatusCode,
                    (await SafeReadContentAsync(response, cancellationToken)).SanitizeLogValue());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while bridging an outbound Telnyx soft-phone call.");
        }
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(TelnyxConstants.ProviderTechnicalName);
        client.BaseAddress = new Uri(_options.ApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        return client;
    }

    private static async Task<string> SafeReadContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return string.Empty;
        }
    }
}
