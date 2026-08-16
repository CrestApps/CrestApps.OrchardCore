using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Core.Indexes;

/// <summary>
/// Indexes subscription content items for lookup, ordering, and version-state filtering.
/// </summary>
public sealed class SubscriptionsContentItemIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the content type of the indexed subscription content item.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the stable content item identifier of the indexed subscription.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the version identifier of the indexed subscription content item.
    /// </summary>
    public string ContentItemVersionId { get; set; }

    /// <summary>
    /// Gets or sets the sort order assigned by the subscription part.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the UTC time at which the content item was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time at which the content item was last modified.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the indexed content item version is published.
    /// </summary>
    public bool Published { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the indexed content item version is the latest version.
    /// </summary>
    public bool Latest { get; set; }
}
