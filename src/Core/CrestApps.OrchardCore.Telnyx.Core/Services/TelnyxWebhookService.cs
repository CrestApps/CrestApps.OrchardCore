using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Provides the default implementation of <see cref="ITelnyxWebhookService"/>. It normalizes Telnyx call
/// events into provider-neutral voice events, projects them through the shared Telephony ingress, and lets
/// optional higher-level features route unmatched inbound calls.
/// </summary>
public sealed partial class TelnyxWebhookService : ITelnyxWebhookService
{
    private readonly INormalizedVoiceEventIngestor _normalizedVoiceEventIngestor;
    private readonly ITelnyxInboundCallRouter _inboundCallRouter;
    private readonly IInboundVoiceDigitsSink _digitsSink;
    private readonly ITelnyxOutboundBridgeOrchestrator _outboundBridgeOrchestrator;
    private readonly IEnumerable<ITelnyxRecordingSavedHandler> _recordingSavedHandlers;
    private readonly IEnumerable<ICallQualityObserver> _callQualityObservers;
    private readonly IExternalTransferOutcomeSink _transferOutcomeSink;
    private readonly TelnyxApiClient _apiClient;
    private readonly IClock _clock;
    private readonly ILogger _logger;

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
    /// <param name="transferOutcomeSink">Where the outcome of an external transfer's destination leg is reported.</param>
    /// <param name="apiClient">The typed Telnyx client, for the hang-up after a last message.</param>
    /// <param name="clock">The clock used to stamp event times.</param>
    /// <param name="logger">The logger.</param>
    public TelnyxWebhookService(
        INormalizedVoiceEventIngestor normalizedVoiceEventIngestor,
        ITelnyxInboundCallRouter inboundCallRouter,
        IInboundVoiceDigitsSink digitsSink,
        ITelnyxOutboundBridgeOrchestrator outboundBridgeOrchestrator,
        IEnumerable<ITelnyxRecordingSavedHandler> recordingSavedHandlers,
        IEnumerable<ICallQualityObserver> callQualityObservers,
        IExternalTransferOutcomeSink transferOutcomeSink,
        TelnyxApiClient apiClient,
        IClock clock,
        ILogger<TelnyxWebhookService> logger)
    {
        _normalizedVoiceEventIngestor = normalizedVoiceEventIngestor;
        _inboundCallRouter = inboundCallRouter;
        _digitsSink = digitsSink;
        _outboundBridgeOrchestrator = outboundBridgeOrchestrator;
        _recordingSavedHandlers = recordingSavedHandlers;
        _callQualityObservers = callQualityObservers;
        _transferOutcomeSink = transferOutcomeSink;
        _apiClient = apiClient;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TelnyxWebhookResult> ProcessAsync(TelnyxCallEvent callEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callEvent);

        // One line per delivery, carrying the provider's own identifiers. Until this existed, which event had
        // arrived for which leg -- and whether it was call.answered or call.bridged -- could only be inferred from
        // the byte size of the webhook body, and an observation made in the browser could not be joined to the
        // leg it happened on except by lining up timestamps.
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Telnyx webhook {EventType}: CallControlId={CallControlId}, Leg={CallLegId}, Session={CallSessionId}, Direction={Direction}, State={State}, HangupCause={HangupCause}, SipHangupCause={SipHangupCause}, ClientState={ClientState}, FailureReason={FailureReason}.",
                callEvent.EventType.SanitizeLogValue(),
                callEvent.CallControlId.SanitizeLogValue(),
                callEvent.CallLegId.SanitizeLogValue(),
                callEvent.CallSessionId.SanitizeLogValue(),
                callEvent.Direction.SanitizeLogValue(),
                callEvent.State.SanitizeLogValue(),
                callEvent.HangupCause.SanitizeLogValue(),
                callEvent.SipHangupCause.SanitizeLogValue(),
                callEvent.ClientState.SanitizeLogValue(),
                callEvent.FailureReason.SanitizeLogValue());
        }

        // Advance an outbound soft-phone bridge before anything else. The destination leg is an internal leg
        // the platform created only to reach the dialed party, so it is bridged here and never surfaced to the
        // soft phone; the agent leg is the call the soft phone tracks, so it continues to normalization below.
        // Every leg's quality is kept, before the hidden bridge leg returns below: that leg is the customer's side of
        // an outbound call, which is the side the agent's soft phone cannot measure.
        await ObserveCallQualityAsync(callEvent, cancellationToken);

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

        // A key press on an entry-point menu. It is not a call-state transition either, and it carries no state token,
        // so it has to be recognised before state mapping: mapped first, it was dropped as unmappable and every menu
        // choice a caller made went nowhere.
        if (string.Equals(callEvent.EventType?.Trim(), TelnyxConstants.Gather.EndedEventType, StringComparison.OrdinalIgnoreCase))
        {
            return await HandleGatherEndedAsync(callEvent, cancellationToken);
        }

        // A leg or a message the platform asked to hear back about: an external transfer's destination leg, or the
        // end of a last message after which the call is hung up.
        if (await HandleCallFlowAsync(callEvent, cancellationToken) is { } callFlowResult)
        {
            return callFlowResult;
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
            Metadata = BuildVoiceEventMetadata(callEvent),
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

    private static Dictionary<string, string> BuildVoiceEventMetadata(TelnyxCallEvent callEvent)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["telnyxEventType"] = callEvent.EventType ?? string.Empty,
            ["telnyxState"] = callEvent.State ?? string.Empty,
            ["telnyxCallSessionId"] = callEvent.CallSessionId ?? string.Empty,
        };

        // The normalized cause collapses several provider outcomes onto one value; what Telnyx actually said is
        // kept alongside it so an ending can be audited in the provider's own terms.
        AddIfPresent(metadata, ContactCenter.ContactCenterConstants.TelephonyMetadata.ProviderHangupCause, callEvent.HangupCause);
        AddIfPresent(metadata, ContactCenter.ContactCenterConstants.TelephonyMetadata.SipHangupCause, callEvent.SipHangupCause);
        AddIfPresent(metadata, ContactCenter.ContactCenterConstants.TelephonyMetadata.HangupSource, callEvent.HangupSource);

        return metadata;
    }

    private static void AddIfPresent(Dictionary<string, string> metadata, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            metadata[key] = value.Trim();
        }
    }

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
