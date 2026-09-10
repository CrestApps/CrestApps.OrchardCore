using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a catalog group used to organize Contact Center queues for administration and reporting.
/// </summary>
public sealed class ActivityQueueGroup : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the unique queue-group name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the queue-group description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the queue group was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the queue group was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
