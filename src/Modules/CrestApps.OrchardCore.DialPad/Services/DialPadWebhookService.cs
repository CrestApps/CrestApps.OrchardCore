using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.DialPad.Services;

/// <summary>
/// Provides the default implementation of <see cref="IDialPadWebhookService"/>. It normalizes DialPad
/// call events into Contact Center provider voice events, updating existing interactions, and routes new
/// inbound calls through the Voice Contact Center Call Router.
/// </summary>
public sealed class DialPadWebhookService : IDialPadWebhookService
{
    private readonly IProviderVoiceEventSink _providerVoiceEventSink;
    private readonly IInboundVoiceEventSink _inboundVoiceEventSink;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialPadWebhookService"/> class.
    /// </summary>
    /// <param name="providerVoiceEventSink">The provider voice event sink used to update existing interactions.</param>
    /// <param name="inboundVoiceEventSink">The inbound voice event sink used to route new calls.</param>
    /// <param name="clock">The clock used to stamp event times.</param>
    public DialPadWebhookService(
        IProviderVoiceEventSink providerVoiceEventSink,
        IInboundVoiceEventSink inboundVoiceEventSink,
        IClock clock)
    {
        _providerVoiceEventSink = providerVoiceEventSink;
        _inboundVoiceEventSink = inboundVoiceEventSink;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<DialPadWebhookResult> ProcessAsync(DialPadCallEvent callEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callEvent);

        if (string.IsNullOrEmpty(callEvent.CallId) || !TryMapState(callEvent.State, out var state))
        {
            return DialPadWebhookResult.Ignored;
        }

        var occurredUtc = callEvent.EventTimestamp.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(callEvent.EventTimestamp.Value).UtcDateTime
            : _clock.UtcNow;

        var toAddress = string.IsNullOrEmpty(callEvent.InternalNumber) ? callEvent.Target : callEvent.InternalNumber;

        var providerEvent = new ProviderVoiceEvent
        {
            ProviderName = DialPadConstants.ProviderTechnicalName,
            ProviderCallId = callEvent.CallId,
            State = state,
            FromAddress = callEvent.ExternalNumber,
            ToAddress = toAddress,
            OccurredUtc = occurredUtc,
            IdempotencyKey = DialPadWebhookDelivery.GetDeliveryId(callEvent),
            IsMuted = callEvent.IsMuted,
            RecordingState = TryMapRecordingState(callEvent.RecordingState, out var recordingState)
                ? recordingState
                : null,
            RecordingReference = callEvent.RecordingId,
            IsConference = callEvent.IsConference,
            ParticipantCount = callEvent.ParticipantCount,
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["dialPadState"] = callEvent.State ?? string.Empty,
            },
        };

        var handled = await _providerVoiceEventSink.IngestAsync(providerEvent, cancellationToken);

        if (handled)
        {
            return DialPadWebhookResult.Updated;
        }

        if (IsInbound(callEvent.Direction) && IsLive(state))
        {
            await _inboundVoiceEventSink.RouteAsync(new InboundVoiceEvent
            {
                ProviderName = DialPadConstants.ProviderTechnicalName,
                ProviderCallId = callEvent.CallId,
                FromAddress = callEvent.ExternalNumber,
                ToAddress = toAddress,
                CallerName = callEvent.ContactName,
                ReceivedUtc = occurredUtc,
            }, cancellationToken);

            return DialPadWebhookResult.Routed;
        }

        return DialPadWebhookResult.Ignored;
    }

    private static bool IsInbound(string direction)
    {
        return string.Equals(direction?.Trim(), "inbound", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLive(ContactCenterCallState state)
    {
        return state is ContactCenterCallState.Dialing or ContactCenterCallState.Ringing or ContactCenterCallState.Connected;
    }

    private static bool TryMapState(string state, out ContactCenterCallState mapped)
    {
        mapped = state?.Trim().ToLowerInvariant() switch
        {
            "calling" or "dialing" or "connecting" or "preanswer" => ContactCenterCallState.Dialing,
            "ringing" => ContactCenterCallState.Ringing,
            "connected" or "active" => ContactCenterCallState.Connected,
            "hold" or "on_hold" or "parked" => ContactCenterCallState.OnHold,
            "hangup" or "ended" or "disconnected" or "completed" or "voicemail" => ContactCenterCallState.Ended,
            "missed" or "no_answer" or "noanswer" => ContactCenterCallState.NoAnswer,
            "rejected" or "declined" or "busy" => ContactCenterCallState.Rejected,
            "canceled" or "cancelled" or "abandoned" => ContactCenterCallState.Canceled,
            "transferred" => ContactCenterCallState.Transferred,
            _ => (ContactCenterCallState)(-1),
        };

        return Enum.IsDefined(mapped);
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
