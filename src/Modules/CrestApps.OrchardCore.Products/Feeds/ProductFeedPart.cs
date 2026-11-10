using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// What a product says to feeds that the product itself doesn't: its brand and identifiers, its condition, its Google
/// category, or that it stays out of them.
/// </summary>
public sealed class ProductFeedPart : ContentPart
{
    public string Brand { get; set; }

    /// <summary>
    /// Gets or sets the GTIN (UPC, EAN, ISBN) of the product, or of each variant by its id.
    /// </summary>
    public string Gtin { get; set; }

    public Dictionary<string, string> VariantGtins { get; set; } = [];

    public string Mpn { get; set; }

    public string Condition { get; set; }

    /// <summary>
    /// Gets or sets the product's Google product category, by name or number, such as <c>Apparel &amp; Accessories &gt; Clothing</c>.
    /// </summary>
    public string GoogleProductCategory { get; set; }

    public bool ExcludeFromFeeds { get; set; }

    /// <summary>
    /// Gets or sets whether AI agents may show the product to buyers, or <see langword="null"/> for the store's default.
    /// </summary>
    public bool? ShowToAgents { get; set; }

    /// <summary>
    /// Gets or sets whether AI agents may check the product out for a buyer, or <see langword="null"/> for the store's
    /// default.
    /// </summary>
    public bool? AgentCheckout { get; set; }
}
