namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Identifies the result of processing a provider voice webhook.
/// </summary>
public enum ProviderVoiceWebhookStatus
{
    /// <summary>
    /// The webhook was accepted and its events were ingested.
    /// </summary>
    Accepted,

    /// <summary>
    /// No adapter is registered for the requested provider.
    /// </summary>
    UnknownProvider,

    /// <summary>
    /// The provider signature failed validation.
    /// </summary>
    InvalidSignature,

    /// <summary>
    /// A parsed event was missing the required idempotency key.
    /// </summary>
    MissingIdempotencyKey,

    /// <summary>
    /// The authenticated provider exceeded its configured delivery rate.
    /// </summary>
    RateLimited,

    /// <summary>
    /// The authenticated delivery omitted a UTC event timestamp or fell outside the accepted freshness window.
    /// </summary>
    StaleDelivery,
}
