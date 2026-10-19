using YesSql.Indexes;

namespace CrestApps.OrchardCore.Products.Core.Indexes;

/// <summary>
/// A product in a bundle: one row per part, so the bundles a product is part of can be found, such as to refresh
/// their stock when the part sells out.
/// </summary>
public sealed class ProductBundleIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the bundle.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the product in it.
    /// </summary>
    public string PartContentItemId { get; set; }

    /// <summary>
    /// Gets or sets how many of the product each bundle holds.
    /// </summary>
    public int Quantity { get; set; }

    public bool Published { get; set; }

    public bool Latest { get; set; }
}
