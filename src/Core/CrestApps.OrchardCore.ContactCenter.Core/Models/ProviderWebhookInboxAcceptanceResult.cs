namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the result of accepting a provider webhook delivery into the durable inbox.
/// </summary>
public sealed class ProviderWebhookInboxAcceptanceResult
{
    /// <summary>
    /// Gets or sets the acceptance status.
    /// </summary>
    public ProviderWebhookInboxAcceptanceStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the durable inbox message identifier when acceptance or duplicate detection succeeded.
    /// </summary>
    public string MessageId { get; set; }
}
