using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Provides the default implementation of <see cref="ITelnyxWebhookService"/>. It normalizes Telnyx call
/// events into provider-neutral voice events, projects them through the shared Telephony ingress, and lets
/// optional higher-level features route unmatched inbound calls.
/// </summary>
public sealed class TelnyxWebhookService : ITelnyxWebhookService
{
    private readonly INormalizedVoiceEventIngestor _normalizedVoiceEventIngestor;
    private readonly ITelnyxInboundCallRouter _inboundCallRouter;
    private readonly ITelnyxOutboundBridgeOrchestrator _outboundBridgeOrchestrator;
    private readonly IEnumerable<ITelnyxRecordingSavedHandler> _recordingSavedHandlers;
    private readonly ITelnyxVoicemailRecordingStarter _voicemailRecordingStarter;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxWebhookService"/> class.
    /// </summary>
    /// <param name="normalizedVoiceEventIngestor">The shared voice-event ingestor.</param>
    /// <param name="inboundCallRouter">The inbound-call router.</param>
    /// <param name="outboundBridgeOrchestrator">The outbound soft-phone bridge orchestrator.</param>
    /// <param name="recordingSavedHandlers">
    /// The optional handlers for finished recordings. When Contact Center Voice is enabled a handler ingests the
    /// recording into the encrypted media store; when none are registered, saved-recording events are ignored.
    /// </param>
    /// <param name="voicemailRecordingStarter">Starts the voicemail recording once its greeting has finished.</param>
    /// <param name="clock">The clock used to stamp event times.</param>
    public TelnyxWebhookService(
        INormalizedVoiceEventIngestor normalizedVoiceEventIngestor,
        ITelnyxInboundCallRouter inboundCallRouter,
        ITelnyxOutboundBridgeOrchestrator outboundBridgeOrchestrator,
        IEnumerable<ITelnyxRecordingSavedHandler> recordingSavedHandlers,
        ITelnyxVoicemailRecordingStarter voicemailRecordingStarter,
        IClock clock)
    {
        _normalizedVoiceEventIngestor = normalizedVoiceEventIngestor;
        _inboundCallRouter = inboundCallRouter;
        _outboundBridgeOrchestrator = outboundBridgeOrchestrator;
        _recordingSavedHandlers = recordingSavedHandlers;
        _voicemailRecordingStarter = voicemailRecordingStarter;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<TelnyxWebhookResult> ProcessAsync(TelnyxCallEvent callEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callEvent);

        // Advance an outbound soft-phone bridge before anything else. The destination leg is an internal leg
        // the platform created only to reach the dialed party, so it is bridged here and never surfaced to the
        // soft phone; the agent leg is the call the soft phone tracks, so it continues to normalization below.
        var bridgeLeg = await _outboundBridgeOrchestrator.AdvanceAsync(callEvent, cancellationToken);

        if (bridgeLeg == TelnyxOutboundBridgeLeg.DestinationLeg)
        {
            return TelnyxWebhookResult.Updated;
        }

        // A finished recording is not a call-state transition, so it is dispatched to the recording handlers
        // before state mapping. When Contact Center Voice is enabled a handler ingests the recording into the
        // encrypted media store; otherwise there are no handlers and the event is ignored below.
        if (string.Equals(callEvent.EventType?.Trim(), TelnyxConstants.Recording.SavedEventType, StringComparison.OrdinalIgnoreCase))
        {
            var recordingHandled = false;

            foreach (var handler in _recordingSavedHandlers)
            {
                recordingHandled |= await handler.HandleAsync(callEvent, cancellationToken);
            }

            return recordingHandled ? TelnyxWebhookResult.Updated : TelnyxWebhookResult.Ignored;
        }

        // The voicemail greeting has finished playing. Its client_state (set on the speak/playback_start command)
        // is echoed here, which is the signal to start the beep-and-record so the greeting is never captured inside
        // the caller's message. Only a greeting-tagged ended event triggers this; any other speak/playback simply
        // falls through and is ignored below.
        var eventType = callEvent.EventType?.Trim();

        if ((string.Equals(eventType, TelnyxConstants.Recording.SpeakEndedEventType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(eventType, TelnyxConstants.Recording.PlaybackEndedEventType, StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrEmpty(callEvent.CallControlId) &&
            TelnyxRecordingClientState.TryParseGreeting(callEvent.ClientState, out var greetingState))
        {
            var started = await _voicemailRecordingStarter.StartAsync(
                callEvent.CallControlId,
                greetingState.InteractionId,
                greetingState.RecipientUserId,
                cancellationToken);

            return started ? TelnyxWebhookResult.Updated : TelnyxWebhookResult.Ignored;
        }

        if (string.IsNullOrEmpty(callEvent.CallControlId) || !TryMapState(callEvent, out var state))
        {
            return TelnyxWebhookResult.Ignored;
        }

        var occurredUtc = callEvent.OccurredUtc ?? _clock.UtcNow;

        var providerEvent = new ProviderVoiceEvent
        {
            ProviderName = TelnyxConstants.ProviderTechnicalName,
            ProviderCallId = callEvent.CallControlId,
            ProviderLegId = callEvent.CallLegId,
            State = state,
            FromAddress = callEvent.From,
            ToAddress = callEvent.To,
            OccurredUtc = occurredUtc,
            IdempotencyKey = TelnyxWebhookDelivery.GetDeliveryId(callEvent),
            RecordingReference = callEvent.RecordingId,
            RecordingState = string.IsNullOrWhiteSpace(callEvent.RecordingId) ? null : Telephony.Models.RecordingState.Stopped,
            HangupCause = ResolveHangupCause(state, callEvent.HangupCause),
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["telnyxEventType"] = callEvent.EventType ?? string.Empty,
                ["telnyxState"] = callEvent.State ?? string.Empty,
                ["telnyxCallSessionId"] = callEvent.CallSessionId ?? string.Empty,
            },
        };

        var handled = await _normalizedVoiceEventIngestor.IngestAsync(providerEvent, cancellationToken);

        if (handled)
        {
            return TelnyxWebhookResult.Updated;
        }

        if (IsInbound(callEvent.Direction) &&
            IsLive(state) &&
            await _inboundCallRouter.RouteAsync(callEvent, occurredUtc, cancellationToken))
        {
            return TelnyxWebhookResult.Routed;
        }

        return TelnyxWebhookResult.Ignored;
    }

    private static bool IsInbound(string direction)
        => string.Equals(direction?.Trim(), "incoming", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(direction?.Trim(), "inbound", StringComparison.OrdinalIgnoreCase);

    private static bool IsLive(VoiceCallState state)
        => state is VoiceCallState.Dialing or VoiceCallState.Ringing or VoiceCallState.Connected;

    private static bool TryMapState(TelnyxCallEvent callEvent, out VoiceCallState mapped)
    {
        // Prefer the event type, which unambiguously describes the transition; fall back to the state token.
        mapped = callEvent.EventType?.Trim().ToLowerInvariant() switch
        {
            "call.initiated" => VoiceCallState.Dialing,
            "call.ringing" => VoiceCallState.Ringing,
            "call.answered" or "call.bridged" => VoiceCallState.Connected,
            "call.hangup" => MapHangup(callEvent.HangupCause),
            _ => MapStateToken(callEvent.State),
        };

        return Enum.IsDefined(mapped);
    }

    private static VoiceCallState MapStateToken(string state)
        => state?.Trim().ToLowerInvariant() switch
        {
            "parked" or "initiated" or "dialing" => VoiceCallState.Dialing,
            "ringing" => VoiceCallState.Ringing,
            "answered" or "active" or "bridged" or "connected" => VoiceCallState.Connected,
            "held" or "hold" => VoiceCallState.OnHold,
            "hangup" or "ended" or "completed" => VoiceCallState.Ended,
            _ => (VoiceCallState)(-1),
        };

    private static VoiceCallState MapHangup(string hangupCause)
        => hangupCause?.Trim().ToLowerInvariant() switch
        {
            "call_rejected" or "user_busy" or "busy" => VoiceCallState.Rejected,
            "no_answer" or "timeout" or "no_user_response" => VoiceCallState.NoAnswer,
            "originator_cancel" or "cancel" => VoiceCallState.Canceled,
            _ => VoiceCallState.Ended,
        };

    private static HangupCause? ResolveHangupCause(VoiceCallState state, string hangupCause)
    {
        return state switch
        {
            VoiceCallState.Ended => Telephony.Models.HangupCause.NormalClearing,
            VoiceCallState.Transferred => Telephony.Models.HangupCause.NormalClearing,
            VoiceCallState.NoAnswer => Telephony.Models.HangupCause.NoAnswer,
            VoiceCallState.Rejected => Telephony.Models.HangupCause.Rejected,
            VoiceCallState.Canceled => Telephony.Models.HangupCause.Canceled,
            VoiceCallState.Failed => Telephony.Models.HangupCause.Failed,
            _ => null,
        };
    }
}
