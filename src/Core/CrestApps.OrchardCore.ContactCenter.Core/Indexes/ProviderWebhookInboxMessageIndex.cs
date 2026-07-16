using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used for provider webhook idempotency and due-message queries.
/// </summary>
public sealed class ProviderWebhookInboxMessageIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the canonical provider technical name.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider-scoped delivery identifier.
    /// </summary>
    public string DeliveryId { get; set; }

    /// <summary>
    /// Gets or sets the durable processing status.
    /// </summary>
    public ProviderWebhookInboxStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the next processing attempt is due.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }

}
