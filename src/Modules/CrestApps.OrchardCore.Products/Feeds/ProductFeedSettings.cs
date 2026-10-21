namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// Settings › Commerce › Product feeds.
/// </summary>
public sealed class ProductFeedSettings
{
    /// <summary>
    /// Gets or sets the brand of products that name none, such as the store's own.
    /// </summary>
    public string DefaultBrand { get; set; }

    public string DefaultCondition { get; set; } = "new";

    public string DefaultGoogleProductCategory { get; set; }

    /// <summary>
    /// Gets or sets whether products that are out of stock stay in the feed, marked so.
    /// </summary>
    public bool IncludeOutOfStock { get; set; } = true;

    /// <summary>
    /// Gets or sets a key the feed's address has to carry, or none for a public feed.
    /// </summary>
    public string Key { get; set; }
}
