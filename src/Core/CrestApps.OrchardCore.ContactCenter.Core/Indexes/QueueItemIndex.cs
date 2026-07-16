using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to query queue items.
/// </summary>
public sealed class QueueItemIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the queue that owns the item.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the CRM activity identifier the item represents.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the unique key that prevents more than one active queue item for an activity.
    /// </summary>
    public string ActivityClaimKey { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle status of the item.
    /// </summary>
    public QueueItemStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the routing priority of the item.
    /// </summary>
    public InteractionPriority Priority { get; set; }

    /// <summary>
    /// Gets or sets the agent assigned to the item.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the item entered the queue.
    /// </summary>
    public DateTime EnqueuedUtc { get; set; }
}
