using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Core.Feeds;

/// <summary>
/// A product feed item being built, with the product and the variant it is.
/// </summary>
public sealed class ProductFeedItemContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductFeedItemContext"/> class.
    /// </summary>
    /// <param name="product">The product's content item.</param>
    /// <param name="variant">The variant the item is, or <see langword="null"/> for a product without variants.</param>
    /// <param name="productType">What kind of product it is, from its content type's settings.</param>
    /// <param name="item">The item being built.</param>
    public ProductFeedItemContext(ContentItem product, ProductVariant variant, ProductType productType, ProductFeedItem item)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(item);

        Product = product;
        Variant = variant;
        ProductType = productType;
        Item = item;
    }

    /// <summary>
    /// Gets the product's content item.
    /// </summary>
    public ContentItem Product { get; }

    /// <summary>
    /// Gets the variant the item is, or <see langword="null"/> for a product without variants.
    /// </summary>
    public ProductVariant Variant { get; }

    /// <summary>
    /// Gets what kind of product it is, such as a good or a download.
    /// </summary>
    public ProductType ProductType { get; }

    /// <summary>
    /// Gets the item being built.
    /// </summary>
    public ProductFeedItem Item { get; }

    /// <summary>
    /// Gets the ISO 4217 code of the item's prices, which amounts a contributor sets are in.
    /// </summary>
    public string Currency
        => Item.Currency;
}
