using YesSql.Indexes;

namespace CrestApps.OrchardCore.Products.Core.Indexes;

/// <summary>
/// A product's SKUs, its own and its variants', one row each, so a product can be found by any of them, such as when a
/// file names products by SKU.
/// </summary>
public sealed class ProductSkuIndex : MapIndex
{
    public const int MaxSkuLength = 255;

    public string ContentItemId { get; set; }

    public string ContentType { get; set; }

    public string Sku { get; set; }

    /// <summary>
    /// Gets or sets the variant the SKU is, or <see langword="null"/> for the product's own.
    /// </summary>
    public string VariantId { get; set; }

    public bool Published { get; set; }

    public bool Latest { get; set; }
}
