using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// The products a bundle or kit is made of, such as a starter kit of a tee and two caps. The bundle is sold at its
/// own price; its stock is what its parts can make, and selling it takes stock from each part.
/// </summary>
public sealed class ProductBundlePart : ContentPart
{
    /// <summary>
    /// The most products a bundle can be made of.
    /// </summary>
    public const int MaxItems = 10;

    public List<ProductBundleItem> Items { get; set; } = [];
}

/// <summary>
/// A product in a bundle, and how many of it each bundle holds.
/// </summary>
public sealed class ProductBundleItem
{
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the product's title when the bundle was saved, for packing lists and order pages.
    /// </summary>
    public string Title { get; set; }

    public int Quantity { get; set; } = 1;
}
