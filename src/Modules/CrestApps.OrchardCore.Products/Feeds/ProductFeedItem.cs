namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// What a product feed says about a product, or about one of its variants: Google Merchant Center's attributes, which
/// Meta and TikTok catalogs read too.
/// </summary>
public sealed class ProductFeedItem
{
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the product a variant belongs to, or none for a product without variants.
    /// </summary>
    public string ItemGroupId { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public string Link { get; set; }

    public string ImageLink { get; set; }

    public List<string> AdditionalImageLinks { get; set; } = [];

    /// <summary>
    /// Gets or sets <c>in_stock</c>, <c>out_of_stock</c>, <c>backorder</c> or <c>preorder</c>.
    /// </summary>
    public string Availability { get; set; }

    public DateTime? AvailabilityDateUtc { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; }

    public decimal? SalePrice { get; set; }

    public DateTime? SaleStartsUtc { get; set; }

    public DateTime? SaleEndsUtc { get; set; }

    public string Brand { get; set; }

    public string Gtin { get; set; }

    public string Mpn { get; set; }

    /// <summary>
    /// Gets or sets <c>new</c>, <c>refurbished</c> or <c>used</c>.
    /// </summary>
    public string Condition { get; set; } = "new";

    public string GoogleProductCategory { get; set; }

    public string ProductType { get; set; }

    public string Color { get; set; }

    public string Size { get; set; }

    /// <summary>
    /// Gets whether the product has a brand and a GTIN or MPN; when it doesn't, the feed says so with
    /// <c>identifier_exists</c>.
    /// </summary>
    public bool IdentifierExists
        => !string.IsNullOrWhiteSpace(Brand) && (!string.IsNullOrWhiteSpace(Gtin) || !string.IsNullOrWhiteSpace(Mpn));
}
