using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to query the durable interaction event history.
/// </summary>
public sealed class InteractionEventIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the interaction the event belongs to.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the canonical event type name.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the type of aggregate the event applies to.
    /// </summary>
    public string AggregateType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the aggregate the event applies to.
    /// </summary>
    public string AggregateId { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier of the event.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the idempotency key used to de-duplicate provider-originated events.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the portable, non-null claim key that enforces idempotency-key uniqueness at the
    /// database level. It is the <see cref="IdempotencyKey"/> when one is present; otherwise it falls
    /// back to the globally unique item identifier so events without a key cannot collide.
    /// </summary>
    public string IdempotencyClaimKey { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the event occurred.
    /// </summary>
    public DateTime OccurredUtc { get; set; }
}
