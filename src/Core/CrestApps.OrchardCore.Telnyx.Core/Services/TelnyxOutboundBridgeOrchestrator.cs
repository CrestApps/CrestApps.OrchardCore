using System.Net.Http.Headers;
using System.Net.Http.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Default implementation of <see cref="ITelnyxOutboundBridgeOrchestrator"/>. It reacts to the
/// <c>call.answered</c> events of the two legs the platform created for a browser-audio outbound call and
/// issues the follow-up Telnyx Call Control commands over REST. All correlation travels in the leg's
/// <c>client_state</c>, so no server-side call registry is required.
/// </summary>
public sealed partial class TelnyxOutboundBridgeOrchestrator : ITelnyxOutboundBridgeOrchestrator
{
    private readonly TelnyxApiClient _apiClient;
    private readonly ILogger<TelnyxOutboundBridgeOrchestrator> _logger;
    private readonly IContactCenterAgentLegFailureService _agentLegFailureService;
    private readonly IEnumerable<ITelnyxAiVoiceEventHandler> _aiVoiceEventHandlers;
    private readonly IAgentPreDialCoordinator _preDialCoordinator;
    private readonly IInboundVoiceInteractionProbe _interactionProbe;
    private readonly IConsultLegEventSink _consultLegEventSink;
    private readonly TelnyxOptions _options;
    private readonly TelnyxTransferCommands _transfers;
    private readonly ITelnyxAgentEndpointResolver _agentEndpointResolver;
    private readonly ISupervisorLegEventSink _supervisorLegEventSink;
    private readonly TelnyxSupervisedConference _supervisedConference;

    public TelnyxOutboundBridgeOrchestrator(
        TelnyxApiClient apiClient,
        ILogger<TelnyxOutboundBridgeOrchestrator> logger,
        IOptionsMonitor<TelnyxOptions> telnyxOptions,
        IContactCenterAgentLegFailureService agentLegFailureService,
        IEnumerable<ITelnyxAiVoiceEventHandler> aiVoiceEventHandlers,
        IEnumerable<IAgentPreDialCoordinator> preDialCoordinators,
        IEnumerable<IInboundVoiceInteractionProbe> interactionProbes,
        IEnumerable<IConsultLegEventSink> consultLegEventSinks = null,
        ITelephonyInteractionStore interactionStore = null,
        IClock clock = null,
        ITelnyxAgentEndpointResolver agentEndpointResolver = null,
        IEnumerable<ISupervisorLegEventSink> supervisorLegEventSinks = null)
    {
        _apiClient = apiClient;
        _logger = logger;
        _options = telnyxOptions.CurrentValue;
        _agentLegFailureService = agentLegFailureService;
        _aiVoiceEventHandlers = aiVoiceEventHandlers;
        _preDialCoordinator = preDialCoordinators?.FirstOrDefault();
        _interactionProbe = interactionProbes?.FirstOrDefault();
        _consultLegEventSink = consultLegEventSinks?.FirstOrDefault();
        _transfers = new TelnyxTransferCommands(apiClient, _options, interactionStore, clock, logger);
        _agentEndpointResolver = agentEndpointResolver;
        _supervisorLegEventSink = supervisorLegEventSinks?.FirstOrDefault();
        _supervisedConference = new TelnyxSupervisedConference(apiClient, logger);
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
            return await AdvanceAiVoiceLegAsync(callEvent, state, cancellationToken);
        }

        if (state.Intent == TelnyxOutboundBridgeState.ContactCenterConsultLegIntent)
        {
            return await AdvanceConsultLegAsync(callEvent, state, isAnswered, cancellationToken);
        }

        if (state.Intent == TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent)
        {
            return await AdvanceSupervisorLegAsync(callEvent, state, isAnswered, cancellationToken);
        }

        if (state.Intent == TelnyxOutboundBridgeState.TransferLegIntent)
        {
            return await AdvanceTransferLegAsync(callEvent, state, isAnswered, cancellationToken);
        }

        if (state.Intent == TelnyxOutboundBridgeState.AgentLegIntent)
        {
            // The agent's browser answered the leg we rang; dial the destination it wanted to reach.
            if (isAnswered && _options.IsConfigured && !string.IsNullOrWhiteSpace(state.Destination))
            {
                await ConnectAgentLegAsync(callEvent.CallControlId, state, cancellationToken);
            }
            else if (IsHangup(callEvent) && _options.IsConfigured)
            {
                await AgentLegEndedAsync(callEvent.CallControlId, state, cancellationToken);
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
                if (_options.IsConfigured &&
                    await BridgeAsync(callControlId: callEvent.CallControlId, otherCallControlId: state.PeerCallControlId, cancellationToken, parkAfterUnbridge: true))
                {
                    // The caller has been listening to the queue while the agent was reached. Now that they are
                    // joined the music stops -- not before, which left the caller in dead air for the second or
                    // more between the agent accepting and their phone answering.
                    await StopCallerPlaybackAsync(state.PeerCallControlId, cancellationToken);
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

            if (!isAnswered && await TryRingContactCenterAgentAgainAsync(callEvent, state, cancellationToken))
            {
                // The agent's phone moved to another credential; the caller waits for the leg rung there instead.
                return TelnyxOutboundBridgeLeg.None;
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
            else if (IsHangup(callEvent))
            {
                await RecordAgentLegEndedAsync(callEvent, state, cancellationToken);
            }

            return TelnyxOutboundBridgeLeg.None;
        }

        if (state.Intent == TelnyxOutboundBridgeState.ContactCenterPreDialedAgentLegIntent)
        {
            // Rung while the offer was ringing. Whether it is joined to the caller is the Contact Center's decision,
            // not this leg's: answering it only proves the agent's browser picked up. Like the accept-time agent leg
            // it is a tracked leg of the interaction, so its events flow on to normalization.
            await AdvancePreDialedAgentLegAsync(callEvent, state, isAnswered, cancellationToken);

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
            else if (await BridgeDialedNumberAsync(agentLegCallControlId: state.PeerCallControlId, destinationLegCallControlId: callEvent.CallControlId, cancellationToken))
            {
                if (!string.IsNullOrWhiteSpace(state.TransferOfCallControlId) && state.Detached != true)
                {
                    // The number a consult dialed answered: the agent can now hand the call to it.
                    await MarkConsultAnsweredAsync(state.PeerCallControlId, cancellationToken);
                }
                else
                {
                    // The number answered: the call can now be merged.
                    await MarkPeerAnsweredAsync(state.PeerCallControlId, cancellationToken);
                }
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

            if (isInternalExtensionCall && await TryRingExtensionTargetAgainAsync(callEvent, state, cancellationToken))
            {
                return TelnyxOutboundBridgeLeg.DestinationLeg;
            }

            // A colleague whose phone is not there is an unavailable extension, which goes to voicemail like one that rang out.
            if ((IsNoAnswerHangup(callEvent) || IsRefusedAsUnavailable(callEvent)) && isInternalExtensionCall)
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
            else if (state.Detached != true && !string.IsNullOrWhiteSpace(state.TransferOfCallControlId))
            {
                // The number a consult dialed hung up: the consult is over, and the call goes back to the agent, held.
                await ConsultTargetLeftAsync(state.PeerCallControlId, state.TransferOfCallControlId, cancellationToken);
            }
            else if (state.Detached != true)
            {
                // The number the agent dialed hung up, answered or not: busy, unanswered, refused, or the end of the
                // conversation. The agent's leg is bridged with park_after_unbridge, so nothing ends it but this.
                await HangupLegAsync(state.PeerCallControlId, cancellationToken);
            }

            // A transfer still ringing for this party, or the outside party it was handed to, has nobody left to talk to.
            await HangupLegAsync(state.PendingTransferCallControlId, cancellationToken);
            await HangupLegAsync(state.ReleaseWithCallControlId, cancellationToken);
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
            var result = await _apiClient.PostCallActionAsync(agentLegCallControlId, "record_start", body, cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogError(
                    "Telnyx rejected starting extension voicemail recording with status code {StatusCode}. Response: {Response}",
                    result.StatusCode,
                    result.ErrorBody.SanitizeLogValue());
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
            // Ensure the conference exists, formed from the active call. The first extension add creates it; a
            // later add finds the existing one. command_id makes a redelivered create idempotent.
            var conferenceId = await EnsureConferenceAsync(conferenceName, state.PeerCallControlId, cancellationToken);

            if (string.IsNullOrWhiteSpace(conferenceId))
            {
                _logger.LogError("Could not resolve the Telnyx conference '{ConferenceName}' to add an extension participant.", conferenceName.SanitizeLogValue());

                return;
            }

            var joinResult = await _apiClient.JoinConferenceAsync(
                conferenceId,
                answeredLegCallControlId,
                endConferenceOnExit: false,
                commandId: $"ext-conf-join-{answeredLegCallControlId}",
                cancellationToken);

            if (!joinResult.Succeeded)
            {
                _logger.LogError(
                    "Telnyx rejected joining an extension participant to conference '{ConferenceName}' with status code {StatusCode}. Response: {Response}",
                    conferenceName.SanitizeLogValue(),
                    joinResult.StatusCode,
                    joinResult.ErrorBody.SanitizeLogValue());
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

    /// <summary>
    /// Returns the conference of that name, creating it when it does not exist yet. The create is attempted
    /// first because it is the common case; a conflict means a prior extension add already made it, and the
    /// deterministic name is what lets this find that one rather than making a second.
    /// </summary>
    private async Task<string> EnsureConferenceAsync(string conferenceName, string activeCallControlId, CancellationToken cancellationToken)
    {
        var created = await _apiClient.CreateConferenceAsync(
            conferenceName,
            activeCallControlId,
            commandId: $"ext-conf-create-{activeCallControlId}",
            cancellationToken);

        if (created.Succeeded && !string.IsNullOrWhiteSpace(created.ConferenceId))
        {
            return created.ConferenceId;
        }

        var existing = await _apiClient.FindConferenceByNameAsync(conferenceName, cancellationToken);

        return existing.Succeeded ? existing.ConferenceId : null;
    }


    private async Task<string> DialDestinationAsync(
        string agentLegCallControlId,
        TelnyxOutboundBridgeState agentState,
        CancellationToken cancellationToken,
        bool redelivered = false)
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
                // A consult's number names the call being consulted about, so its answer and its hang-up are read as
                // the consult's.
                TransferOfCallControlId = agentState.ConsultOfCallControlId,
                Redelivered = redelivered ? true : null,
            }.ToClientState(),
            // Telnyx de-duplicates by command_id, so a redelivered agent-answered webhook cannot place a
            // second destination call. The one leg rung again after a refusal has an id of its own.
            ["command_id"] = redelivered ? $"ob-dest-again-{agentLegCallControlId}" : $"ob-dest-{agentLegCallControlId}",
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

        // A colleague's soft phone is rung on this leg, and must ring it rather than answer it as a leg of its own. The
        // SDK may hand the phone no client state, so the leg says what it is in a SIP header too.
        if (destinationIsInternalSip)
        {
            body["custom_headers"] = new[]
            {
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["name"] = TelnyxTransferCommands.DestinationLegSipHeader,
                    ["value"] = "1",
                },
            };
        }

        // Bound the ring so an unanswered internal extension call is released and can fall to voicemail rather
        // than ringing indefinitely.
        if (agentState.RingTimeoutSeconds is > 0)
        {
            body["timeout_secs"] = agentState.RingTimeoutSeconds.Value;
        }

        try
        {
            var originate = new TelnyxOriginateRequest();

            // The body was already assembled above in the provider's own shape, so it is carried straight
            // through rather than unpacked into named fields and packed again.
            foreach (var field in body)
            {
                originate.AdditionalFields[field.Key] = field.Value;
            }

            var result = await _apiClient.OriginateAsync(originate, cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogError(
                    "Telnyx rejected the destination leg of an outbound bridge with status code {StatusCode}. Response: {Response}",
                    result.StatusCode,
                    result.ErrorBody.SanitizeLogValue());

                return null;
            }

            return result.CallControlId;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while dialing the destination leg of a Telnyx outbound bridge.");

            return null;
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
            // A leg that has already ended fails here; that is expected (for example the caller hung up first,
            // which ended the conference and this leg), so the result is deliberately not inspected.
            await _apiClient.HangupAsync(callControlId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occurred while hanging up the peer leg {CallControlId} of a bridged call.", callControlId.SanitizeLogValue());
        }
    }

    private async Task StopCallerPlaybackAsync(string callerCallControlId, CancellationToken cancellationToken)
    {
        try
        {
            // Idempotent: a caller with nothing playing is refused (422) and that is the expected answer.
            var result = await _apiClient.StopPlaybackAsync(callerCallControlId, cancellationToken);

            if (!result.Succeeded && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Telnyx returned {StatusCode} stopping the caller's hold music after an agent bridge (nothing may have been playing).",
                    result.StatusCode);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occurred while stopping the caller's hold music after an agent bridge.");
        }
    }

    // The bridge is issued on callControlId, so park_after_unbridge applies to that leg.
    private async Task<bool> BridgeAsync(string callControlId, string otherCallControlId, CancellationToken cancellationToken, bool parkAfterUnbridge = false)
    {
        var body = new Dictionary<string, object>
        {
            ["call_control_id"] = otherCallControlId,
            ["command_id"] = $"ob-bridge-{callControlId}",
        };

        if (parkAfterUnbridge)
        {
            body["park_after_unbridge"] = "self";
        }

        try
        {
            var result = await _apiClient.PostCallActionAsync(callControlId, "bridge", body, cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogError(
                    "Telnyx rejected the bridge of an outbound soft-phone call with status code {StatusCode}. Response: {Response}",
                    result.StatusCode,
                    result.ErrorBody.SanitizeLogValue());
            }

            return result.Succeeded;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while bridging an outbound Telnyx soft-phone call.");

            return false;
        }
    }
}
