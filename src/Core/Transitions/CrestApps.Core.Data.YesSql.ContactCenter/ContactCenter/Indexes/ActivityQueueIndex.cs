using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.ContactCenter.Models;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Indexes;

/// <summary>
/// Represents the YesSql index used to query queues.
/// </summary>
public sealed class ActivityQueueIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the unique name of the queue.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the optional queue-group identifier.
    /// </summary>
    public string QueueGroupId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the queue is enabled for routing.
    /// </summary>
    public bool Enabled { get; set; }
}
