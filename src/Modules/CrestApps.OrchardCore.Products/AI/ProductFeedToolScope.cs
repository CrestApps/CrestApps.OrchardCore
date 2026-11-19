namespace CrestApps.OrchardCore.Products.AI;

/// <summary>
/// Whether the feed's access key was made or removed in the current chat message. The site's settings are read from the
/// cache until the message's service scope ends, so the feed status reads this record instead of the stale settings.
/// </summary>
internal sealed class ProductFeedToolScope
{
    /// <summary>
    /// Gets or sets whether the feed has an access key after a change in this message, or <see langword="null"/> when
    /// it was not changed.
    /// </summary>
    public bool? HasKey { get; set; }
}
