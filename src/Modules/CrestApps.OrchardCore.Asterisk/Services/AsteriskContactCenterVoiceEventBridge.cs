using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskContactCenterVoiceEventBridge : IAsteriskRealtimeVoiceEventBridge
{
    private readonly IProviderVoiceEventSink _providerVoiceEventSink;
    private readonly ILogger<AsteriskContactCenterVoiceEventBridge> _logger;

    public AsteriskContactCenterVoiceEventBridge(
        IProviderVoiceEventSink providerVoiceEventSink,
        ILogger<AsteriskContactCenterVoiceEventBridge> logger)
    {
        _providerVoiceEventSink = providerVoiceEventSink;
        _logger = logger;
    }

    public async Task<bool> TryHandleAsync(
        AsteriskRealtimeVoiceEvent voiceEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voiceEvent);

        var handled = await _providerVoiceEventSink.IngestAsync(BuildProviderVoiceEvent(voiceEvent), cancellationToken);

        if (!handled)
        {
            return false;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Asterisk real-time event {EventType} for provider {ProviderName} call {CallId} flowed into Contact Center.",
                OperationalLogRedactor.Redact(voiceEvent.EventType, OperationalLogFieldKind.FreeText),
                voiceEvent.ProviderName,
                OperationalLogRedactor.Pseudonymize(voiceEvent.CallId, OperationalLogIdentifierCategory.Call));
        }

        return true;
    }

    private static ProviderVoiceEvent BuildProviderVoiceEvent(AsteriskRealtimeVoiceEvent voiceEvent)
    {
        return new ProviderVoiceEvent
        {
            ProviderName = voiceEvent.ProviderName,
            ProviderCallId = voiceEvent.CallId,
            State = ToContactCenterState(voiceEvent.State),
            FromAddress = voiceEvent.FromAddress,
            ToAddress = voiceEvent.ToAddress,
            OccurredUtc = voiceEvent.OccurredUtc,
            IdempotencyKey = voiceEvent.IdempotencyKey,
            IsMuted = voiceEvent.IsMuted,
            IsConference = voiceEvent.IsConference,
            ParticipantCount = voiceEvent.ParticipantCount,
            Metadata = new Dictionary<string, string>(voiceEvent.Metadata, StringComparer.OrdinalIgnoreCase),
        };
    }

    private static ContactCenterCallState ToContactCenterState(CallState state)
    {
        return state switch
        {
            CallState.Connecting => ContactCenterCallState.Dialing,
            CallState.Ringing => ContactCenterCallState.Ringing,
            CallState.Connected => ContactCenterCallState.Connected,
            CallState.OnHold => ContactCenterCallState.OnHold,
            CallState.Disconnected => ContactCenterCallState.Ended,
            CallState.Failed => ContactCenterCallState.Failed,
            _ => ContactCenterCallState.Dialing,
        };
    }
}
