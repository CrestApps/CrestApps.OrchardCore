namespace CrestApps.OrchardCore.Products.Core.Feeds;

/// <summary>
/// The product, or variant of one, that a feed item id names.
/// </summary>
public sealed class ProductItemReference
{
    /// <summary>
    /// Gets or sets the item's id as product feeds give it (see <see cref="ProductFeedItemIds.Create"/>).
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the content item id of the product.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the variant's id, or <see langword="null"/> for a product without variants.
    /// </summary>
    public string VariantId { get; set; }

    /// <summary>
    /// Gets or sets the SKU of the variant, or of the product without variants, or <see langword="null"/>.
    /// </summary>
    public string Sku { get; set; }
}
