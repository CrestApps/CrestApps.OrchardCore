using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskRealtimeVoiceEventDispatcher
{
    private readonly IEnumerable<IAsteriskRealtimeVoiceEventBridge> _voiceEventBridges;
    private readonly IEnumerable<IAsteriskCallTeardownService> _callTeardownServices;
    private readonly INormalizedVoiceEventIngestor _normalizedVoiceEventIngestor;
    private readonly ILogger<AsteriskRealtimeVoiceEventDispatcher> _logger;

    public AsteriskRealtimeVoiceEventDispatcher(
        IEnumerable<IAsteriskRealtimeVoiceEventBridge> voiceEventBridges,
        IEnumerable<IAsteriskCallTeardownService> callTeardownServices,
        INormalizedVoiceEventIngestor normalizedVoiceEventIngestor,
        ILogger<AsteriskRealtimeVoiceEventDispatcher> logger)
    {
        _voiceEventBridges = voiceEventBridges;
        _callTeardownServices = callTeardownServices;
        _normalizedVoiceEventIngestor = normalizedVoiceEventIngestor;
        _logger = logger;
    }

    public async Task HandleAsync(AsteriskRealtimeVoiceEvent voiceEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voiceEvent);

        if (string.IsNullOrWhiteSpace(voiceEvent.ProviderName) || string.IsNullOrWhiteSpace(voiceEvent.CallId))
        {
            return;
        }

        var absorbedByBridge = false;

        try
        {
            foreach (var voiceEventBridge in _voiceEventBridges)
            {
                if (await voiceEventBridge.TryHandleAsync(voiceEvent, cancellationToken))
                {
                    absorbedByBridge = true;

                    break;
                }
            }
        }
        finally
        {
            // Terminal resource cleanup runs in a finally so it happens regardless of which bridge (if any)
            // claimed the event and even if a bridge throws, because releasing ARI bridges, channels, and
            // ownership bindings is orthogonal to projecting call status. Each service is a no-op for
            // non-terminal events and for channels the current tenant does not own.
            foreach (var callTeardownService in _callTeardownServices)
            {
                await callTeardownService.ReleaseAsync(voiceEvent, cancellationToken);
            }
        }

        if (absorbedByBridge)
        {
            return;
        }

        // Everything that survives call-control interception is normalized once and ingested once. The
        // ingestor takes the ingestion lease and fans the event out to every projection, so telephony call
        // history and Contact Center both observe it instead of racing to claim it first.
        var handled = await _normalizedVoiceEventIngestor.IngestAsync(
            BuildProviderVoiceEvent(voiceEvent),
            cancellationToken);

        if (!handled && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Asterisk real-time event {EventType} for provider {ProviderName} call {CallId} was not projected by any consumer.",
                OperationalLogRedactor.Redact(voiceEvent.EventType, OperationalLogFieldKind.FreeText),
                voiceEvent.ProviderName,
                OperationalLogRedactor.Pseudonymize(voiceEvent.CallId, OperationalLogIdentifierCategory.Call));
        }
    }

    private static ProviderVoiceEvent BuildProviderVoiceEvent(AsteriskRealtimeVoiceEvent voiceEvent)
    {
        return new ProviderVoiceEvent
        {
            ProviderName = voiceEvent.ProviderName,
            ProviderCallId = voiceEvent.CallId,
            State = VoiceCallStateProjection.ToVoiceCallState(
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
