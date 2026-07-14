namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the outcome of processing a provider voice webhook.
/// </summary>
public sealed class ProviderVoiceWebhookOutcome
{
    /// <summary>
    /// Gets or sets the status of the webhook processing.
    /// </summary>
    public ProviderVoiceWebhookStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the number of normalized events that were ingested.
    /// </summary>
    public int ProcessedCount { get; set; }

    /// <summary>
    /// Gets or sets the suggested delay before a rate-limited delivery retries.
    /// </summary>
    public TimeSpan? RetryAfter { get; set; }
}
