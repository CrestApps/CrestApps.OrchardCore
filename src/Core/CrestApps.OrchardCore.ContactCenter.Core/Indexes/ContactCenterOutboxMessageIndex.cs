using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to query Contact Center outbox messages that are due for retry.
/// </summary>
public sealed class ContactCenterOutboxMessageIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the event the message retries.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the dispatch state of the message.
    /// </summary>
    public OutboxMessageStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the next dispatch attempt is due.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }
}
