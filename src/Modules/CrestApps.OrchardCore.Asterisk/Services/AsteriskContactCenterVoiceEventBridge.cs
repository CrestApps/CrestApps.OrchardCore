using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;
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
            State = ContactCenterCallStateProjection.ToContactCenterCallState(
                voiceEvent.State,
                voiceEvent.IsOnHold,
                voiceEvent.HangupCause),
            HangupCause = voiceEvent.HangupCause,
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
}
