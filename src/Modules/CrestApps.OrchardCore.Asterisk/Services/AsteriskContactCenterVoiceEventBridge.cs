using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskContactCenterVoiceEventBridge : IAsteriskRealtimeVoiceEventBridge
{
    private readonly IProviderVoiceEventService _providerVoiceEventService;
    private readonly ILogger<AsteriskContactCenterVoiceEventBridge> _logger;

    public AsteriskContactCenterVoiceEventBridge(
        IProviderVoiceEventService providerVoiceEventService,
        ILogger<AsteriskContactCenterVoiceEventBridge> logger)
    {
        _providerVoiceEventService = providerVoiceEventService;
        _logger = logger;
    }

    public async Task<bool> TryHandleAsync(
        AsteriskRealtimeVoiceEvent voiceEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voiceEvent);

        var session = await _providerVoiceEventService.IngestAsync(BuildProviderVoiceEvent(voiceEvent), cancellationToken);

        if (session is null)
        {
            return false;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Asterisk real-time event {EventType} for provider {ProviderName} call {CallId} flowed into Contact Center session {SessionId}.",
                OperationalLogRedactor.Redact(voiceEvent.EventType, OperationalLogFieldKind.FreeText),
                voiceEvent.ProviderName,
                OperationalLogRedactor.Pseudonymize(voiceEvent.CallId, OperationalLogIdentifierCategory.Call),
                OperationalLogRedactor.Pseudonymize(session.ItemId, OperationalLogIdentifierCategory.Session));
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
