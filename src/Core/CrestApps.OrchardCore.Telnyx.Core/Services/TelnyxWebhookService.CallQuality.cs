using System.Globalization;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Hands Telnyx's measurement of each ended leg to whatever keeps a record of call quality.
/// </summary>
public sealed partial class TelnyxWebhookService
{
    private async Task ObserveCallQualityAsync(TelnyxCallEvent callEvent, CancellationToken cancellationToken)
    {
        var stats = callEvent.CallQualityStats;

        if (stats is null)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            // Skipped slots are not packet loss (silence and one-way media leave them too), so they are logged as what
            // they are and the leg is rated on the provider's opinion score.
            _logger.LogInformation(
                "Telnyx call quality for leg {CallControlId}: Mos={Mos}, Skipped={SkippedPercent}%, JitterMaxVariance={JitterMaxVariance}, InboundPackets={InboundPackets}, InboundSkipped={InboundSkipped}, OutboundPackets={OutboundPackets}, OutboundSkipped={OutboundSkipped}.",
                callEvent.CallControlId.SanitizeLogValue(),
                stats.InboundMos,
                stats.InboundSkippedPercent?.ToString("0.##", CultureInfo.InvariantCulture),
                stats.InboundJitterMaxVarianceMs,
                stats.InboundPacketCount,
                stats.InboundSkipPacketCount,
                stats.OutboundPacketCount,
                stats.OutboundSkipPacketCount);
        }

        var observation = new CallQualityObservation
        {
            Source = CallQualitySource.Provider,
            ProviderName = TelnyxConstants.ProviderTechnicalName,
            ProviderCallControlId = callEvent.CallControlId,
            ProviderLegId = callEvent.CallLegId,
            ProviderSessionId = callEvent.CallSessionId,
            Rating = TelephonyCallQualityEvaluator.EvaluateProvider(stats),
            ObservedUtc = callEvent.OccurredUtc ?? _clock.UtcNow,
            Provider = stats,
        };

        foreach (var observer in _callQualityObservers)
        {
            try
            {
                await observer.ObserveAsync(observation, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A quality record that could not be kept must not cost the hangup it arrived with.
                _logger.LogWarning(
                    ex,
                    "The Telnyx call quality for leg {CallControlId} could not be recorded by {Observer}.",
                    callEvent.CallControlId.SanitizeLogValue(),
                    observer.GetType().Name.SanitizeLogValue());
            }
        }
    }
}
