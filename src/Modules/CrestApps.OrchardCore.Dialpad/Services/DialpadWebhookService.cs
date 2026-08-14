using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Provides the default implementation of <see cref="IDialpadWebhookService"/>. It normalizes Dialpad
/// call events into Contact Center provider voice events, updating existing interactions, and routes new
/// inbound calls through the Voice Contact Center Call Router.
/// </summary>
public sealed class DialpadWebhookService : IDialpadWebhookService
{
    private readonly IProviderVoiceEventSink _providerVoiceEventSink;
    private readonly IInboundVoiceEventSink _inboundVoiceEventSink;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialpadWebhookService"/> class.
    /// </summary>
    /// <param name="providerVoiceEventSink">The provider voice event sink used to update existing interactions.</param>
    /// <param name="inboundVoiceEventSink">The inbound voice event sink used to route new calls.</param>
    /// <param name="clock">The clock used to stamp event times.</param>
    public DialpadWebhookService(
        IProviderVoiceEventSink providerVoiceEventSink,
        IInboundVoiceEventSink inboundVoiceEventSink,
        IClock clock)
    {
        _providerVoiceEventSink = providerVoiceEventSink;
        _inboundVoiceEventSink = inboundVoiceEventSink;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<DialpadWebhookResult> ProcessAsync(DialpadCallEvent callEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callEvent);

        if (string.IsNullOrEmpty(callEvent.CallId) || !TryMapState(callEvent.State, out var state))
        {
            return DialpadWebhookResult.Ignored;
        }

        var occurredUtc = callEvent.EventTimestamp.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(callEvent.EventTimestamp.Value).UtcDateTime
            : _clock.UtcNow;

        var toAddress = string.IsNullOrEmpty(callEvent.InternalNumber) ? callEvent.Target : callEvent.InternalNumber;
        var answerClassification = TryMapAnswerClassification(callEvent.State, out var amdClassification)
            ? amdClassification
            : (AnswerClassification?)null;

        var providerEvent = new ProviderVoiceEvent
        {
            ProviderName = DialpadConstants.ProviderTechnicalName,
            ProviderCallId = callEvent.CallId,
            State = state,
            FromAddress = callEvent.ExternalNumber,
            ToAddress = toAddress,
            OccurredUtc = occurredUtc,
            IdempotencyKey = DialpadWebhookDelivery.GetDeliveryId(callEvent),
            IsMuted = callEvent.IsMuted,
            RecordingState = TryMapRecordingState(callEvent.RecordingState, out var recordingState)
                ? recordingState
                : null,
            RecordingReference = callEvent.RecordingId,
            IsConference = callEvent.IsConference,
            ParticipantCount = callEvent.ParticipantCount,
            AnswerClassification = answerClassification,
            HangupCause = ResolveHangupCause(state, answerClassification),
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["dialPadState"] = callEvent.State ?? string.Empty,
            },
        };

        var handled = await _providerVoiceEventSink.IngestAsync(providerEvent, cancellationToken);

        if (handled)
        {
            return DialpadWebhookResult.Updated;
        }

        if (IsInbound(callEvent.Direction) && IsLive(state))
        {
            await _inboundVoiceEventSink.RouteAsync(new InboundVoiceEvent
            {
                ProviderName = DialpadConstants.ProviderTechnicalName,
                ProviderCallId = callEvent.CallId,
                FromAddress = callEvent.ExternalNumber,
                ToAddress = toAddress,
                CallerName = callEvent.ContactName,
                ReceivedUtc = occurredUtc,
            }, cancellationToken);

            return DialpadWebhookResult.Routed;
        }

        return DialpadWebhookResult.Ignored;
    }

    private static bool IsInbound(string direction)
    {
        return string.Equals(direction?.Trim(), "inbound", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLive(VoiceCallState state)
    {
        return state is VoiceCallState.Dialing or VoiceCallState.Ringing or VoiceCallState.Connected;
    }

    private static bool TryMapState(string state, out VoiceCallState mapped)
    {
        mapped = state?.Trim().ToLowerInvariant() switch
        {
            "calling" or "dialing" or "connecting" or "preanswer" => VoiceCallState.Dialing,
            "ringing" => VoiceCallState.Ringing,
            "connected" or "active" or "human" or "live" => VoiceCallState.Connected,
            "hold" or "on_hold" or "parked" => VoiceCallState.OnHold,
            "hangup" or "ended" or "disconnected" or "completed" or "voicemail"
                or "voicemail_greeting" or "machine" or "answering_machine" or "fax" or "fax_detected" => VoiceCallState.Ended,
            "missed" or "no_answer" or "noanswer" => VoiceCallState.NoAnswer,
            "rejected" or "declined" or "busy" => VoiceCallState.Rejected,
            "canceled" or "cancelled" or "abandoned" => VoiceCallState.Canceled,
            "transferred" => VoiceCallState.Transferred,
            _ => (VoiceCallState)(-1),
        };

        return Enum.IsDefined(mapped);
    }

    // Dialpad reports no release cause of its own; its call-state token already carries the outcome, so the
    // provider-neutral cause is derived from the normalized state rather than from a second token vocabulary
    // that would have to be kept in step with the first. A completed call that answer detection classified as
    // a machine or a fax is the one case the state alone cannot express.
    private static HangupCause? ResolveHangupCause(VoiceCallState state, AnswerClassification? answerClassification)
    {
        if (answerClassification is AnswerClassification.Machine or AnswerClassification.Fax)
        {
            return HangupCause.AnsweringMachine;
        }

        return state switch
        {
            VoiceCallState.Ended => HangupCause.NormalClearing,
            VoiceCallState.Transferred => HangupCause.NormalClearing,
            VoiceCallState.NoAnswer => HangupCause.NoAnswer,
            VoiceCallState.Rejected => HangupCause.Rejected,
            VoiceCallState.Canceled => HangupCause.Canceled,
            VoiceCallState.Failed => HangupCause.Failed,
            _ => null,
        };
    }

    private static bool TryMapAnswerClassification(string state, out AnswerClassification classification)
    {
        classification = state?.Trim().ToLowerInvariant() switch
        {
            "voicemail" or "voicemail_greeting" or "machine" or "answering_machine" => AnswerClassification.Machine,
            "fax" or "fax_detected" => AnswerClassification.Fax,
            "human" or "live" => AnswerClassification.Human,
            _ => (AnswerClassification)(-1),
        };

        return Enum.IsDefined(classification);
    }

    private static bool TryMapRecordingState(string state, out RecordingState mapped)
    {
        mapped = state?.Trim().ToLowerInvariant() switch
        {
            "recording" or "started" or "active" => RecordingState.Recording,
            "paused" => RecordingState.Paused,
            "stopped" or "completed" => RecordingState.Stopped,
            "none" or "not_recording" => RecordingState.None,
            _ => (RecordingState)(-1),
        };

        return Enum.IsDefined(mapped);
    }
}
