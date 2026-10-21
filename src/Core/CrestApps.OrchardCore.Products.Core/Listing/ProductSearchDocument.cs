using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// The denormalized search record of one published product.
/// </summary>
/// <remarks>
/// Filtering a listing over a dozen generic field tables with dynamic joins neither scales nor counts facets
/// cheaply. One record per product, with every attribute value flattened into rows, does both, and it is also
/// the document a search-server provider would index, so every provider reads the same shape.
/// </remarks>
public sealed class ProductSearchDocument
{
    /// <summary>
    /// Gets or sets the YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the product's content item identifier.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the published version the record was built from.
    /// </summary>
    public string ContentItemVersionId { get; set; }

    /// <summary>
    /// Gets or sets the product's content type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the culture of a localized product, such as <c>fr-FR</c>, or <see langword="null"/> for a product
    /// that is not localized and shows in every culture.
    /// </summary>
    public string Culture { get; set; }

    /// <summary>
    /// Gets or sets the product's title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the product's stock-keeping unit.
    /// </summary>
    public string Sku { get; set; }

    /// <summary>
    /// Gets or sets the price, when the product has one.
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// Gets or sets the compare-at price, when the product has one.
    /// </summary>
    public decimal? CompareAtPrice { get; set; }

    /// <summary>
    /// Gets or sets when the product's sale starts, when it has a sale price, so the record is built again then.
    /// </summary>
    public DateTime? SaleStartsUtc { get; set; }

    /// <summary>
    /// Gets or sets when the product's sale ends, when it has a sale price, so the record is built again then.
    /// </summary>
    public DateTime? SaleEndsUtc { get; set; }

    /// <summary>
    /// Gets or sets the highest price of a product whose variants are priced differently, so a card can show
    /// "From" the lowest. <see langword="null"/> when every price is the same.
    /// </summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>
    /// Gets or sets the currency the prices are expressed in.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets how the price is shown.
    /// </summary>
    public ProductPriceDisplay PriceDisplay { get; set; }

    /// <summary>
    /// Gets or sets how often the price is charged, for a product sold by subscription, or
    /// <see langword="null"/> for a one-time price.
    /// </summary>
    public BillingInterval? BillingInterval { get; set; }

    /// <summary>
    /// Gets or sets how many intervals one billing period spans, such as 3 for a price charged every 3 months.
    /// </summary>
    public int? BillingDuration { get; set; }

    /// <summary>
    /// Gets or sets whether the product can be bought online.
    /// </summary>
    public bool IsPurchasable { get; set; }

    /// <summary>
    /// Gets or sets whether the merchant features the product.
    /// </summary>
    public bool Featured { get; set; }

    /// <summary>
    /// Gets or sets the product's place in the order the merchant arranged, or <see langword="null"/> when it has none.
    /// </summary>
    public int? Position { get; set; }

    /// <summary>
    /// Gets or sets when the product was first published.
    /// </summary>
    public DateTime? PublishedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the product was created.
    /// </summary>
    public DateTime? CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the media path of the product's first image, shown on its card.
    /// </summary>
    public string ImagePath { get; set; }

    /// <summary>
    /// Gets or sets the product's attribute values, one entry per value.
    /// </summary>
    public List<ProductAttributeValue> Attributes { get; set; } = [];
}
