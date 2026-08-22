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

    /// <summary>
    /// Gets or sets the UTC time the delivery reached a terminal outcome. Retention purges settled deliveries by
    /// this age. Receipt time cannot serve, because settlement lags receipt by the whole retry envelope and would
    /// shorten the redelivery tombstone below its guarantee; the retry time cannot serve either, because a settled
    /// delivery keeps whatever retry time it last held.
    /// </summary>
    public DateTime? ProcessedUtc { get; set; }

}
