using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Derives a stable delivery identifier for a Telnyx call event so duplicate webhook deliveries are
/// de-duplicated by the durable inbox and the normalized voice-event ingestor.
/// </summary>
internal static class TelnyxWebhookDelivery
{
    public static string GetDeliveryId(TelnyxCallEvent callEvent)
    {
        ArgumentNullException.ThrowIfNull(callEvent);

        // Telnyx supplies a unique event id per delivery; prefer it. When it is absent, fall back to a hash
        // of the identifying fields so replays still collapse to the same delivery id.
        if (!string.IsNullOrWhiteSpace(callEvent.EventId))
        {
            return $"{TelnyxConstants.ProviderTechnicalName}:{callEvent.EventId}";
        }

        var value = string.Join(
            '|',
            callEvent.CallControlId,
            callEvent.EventType,
            callEvent.State,
            callEvent.OccurredUtc?.ToString("O"));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return $"{TelnyxConstants.ProviderTechnicalName}:{Convert.ToHexString(hash)}";
    }
}
