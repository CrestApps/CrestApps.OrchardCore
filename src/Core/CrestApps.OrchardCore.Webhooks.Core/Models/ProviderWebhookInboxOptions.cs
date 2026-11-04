namespace CrestApps.OrchardCore.Webhooks.Core.Models;

/// <summary>
/// Configures the provider webhook inbox.
/// </summary>
public sealed class ProviderWebhookInboxOptions
{
    /// <summary>
    /// Gets or sets how many days a completed delivery is kept as an idempotency tombstone. The inbox never keeps one
    /// for less than its own redelivery window, so a value below it, or zero, keeps that window.
    /// </summary>
    public double RetentionDays { get; set; }
}
