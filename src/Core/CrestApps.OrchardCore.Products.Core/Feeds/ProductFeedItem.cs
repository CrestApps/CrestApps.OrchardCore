namespace CrestApps.OrchardCore.Products.Core.Feeds;

/// <summary>
/// What a product feed says about a product, or about one of its variants: Google Merchant Center's attributes, which
/// Meta and TikTok catalogs read too, and what AI agents' feeds add (stock, seller, policies, returns, size and weight).
/// Modules add what they own through <see cref="IProductFeedItemContributor"/>.
/// </summary>
public sealed class ProductFeedItem
{
    /// <summary>
    /// Gets or sets the item's id: its SKU, else the product's id, with the variant's after it. Checkouts accept it as
    /// the item to buy (see <see cref="ProductFeedItemIds"/>).
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the product a variant belongs to, or none for a product without variants.
    /// </summary>
    public string ItemGroupId { get; set; }

    /// <summary>
    /// Gets or sets the content item id of the product.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the variant the item is, or <see langword="null"/> for a product without variants.
    /// </summary>
    public string VariantId { get; set; }

    /// <summary>
    /// Gets or sets the item's title: the product's, with the variant's after it.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the product's own title, without the variant's.
    /// </summary>
    public string ProductTitle { get; set; }

    /// <summary>
    /// Gets or sets the item's description, as plain text.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the absolute address of the product's page.
    /// </summary>
    public string Link { get; set; }

    /// <summary>
    /// Gets or sets the absolute address of the item's main photo.
    /// </summary>
    public string ImageLink { get; set; }

    /// <summary>
    /// Gets or sets the absolute addresses of the product's other photos.
    /// </summary>
    public List<string> AdditionalImageLinks { get; set; } = [];

    /// <summary>
    /// Gets or sets <c>in_stock</c>, <c>out_of_stock</c>, <c>backorder</c> or <c>preorder</c>.
    /// </summary>
    public string Availability { get; set; }

    /// <summary>
    /// Gets or sets when a pre-order or backorder ships, or <see langword="null"/>.
    /// </summary>
    public DateTime? AvailabilityDateUtc { get; set; }

    /// <summary>
    /// Gets or sets the units that can be bought now, or <see langword="null"/> when the store doesn't count them.
    /// </summary>
    public int? AvailableQuantity { get; set; }

    /// <summary>
    /// Gets or sets the price anyone pays, in <see cref="Currency"/>.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Gets or sets the ISO 4217 code of the item's prices.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the price during a sale that is on or coming, or <see langword="null"/>.
    /// </summary>
    public decimal? SalePrice { get; set; }

    /// <summary>
    /// Gets or sets when the sale starts, or <see langword="null"/>.
    /// </summary>
    public DateTime? SaleStartsUtc { get; set; }

    /// <summary>
    /// Gets or sets when the sale ends, or <see langword="null"/>.
    /// </summary>
    public DateTime? SaleEndsUtc { get; set; }

    /// <summary>
    /// Gets or sets the product's brand.
    /// </summary>
    public string Brand { get; set; }

    /// <summary>
    /// Gets or sets the item's GTIN (UPC, EAN, ISBN), or <see langword="null"/>.
    /// </summary>
    public string Gtin { get; set; }

    /// <summary>
    /// Gets or sets the maker's part number, or <see langword="null"/>.
    /// </summary>
    public string Mpn { get; set; }

    /// <summary>
    /// Gets or sets <c>new</c>, <c>refurbished</c> or <c>used</c>.
    /// </summary>
    public string Condition { get; set; } = "new";

    /// <summary>
    /// Gets or sets the product's Google product category, by name or number.
    /// </summary>
    public string GoogleProductCategory { get; set; }

    /// <summary>
    /// Gets or sets the store's own category of the product, such as <c>Apparel &gt; Shoes</c>.
    /// </summary>
    public string ProductType { get; set; }

    /// <summary>
    /// Gets or sets the item's color, or <see langword="null"/>.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets the item's size, or <see langword="null"/>.
    /// </summary>
    public string Size { get; set; }

    /// <summary>
    /// Gets or sets a variant's options by name, such as <c>Color: Black</c> and <c>Size: 10</c>; empty for a product
    /// without variants.
    /// </summary>
    public IDictionary<string, string> VariantOptions { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the name of the store that sells the item.
    /// </summary>
    public string SellerName { get; set; }

    /// <summary>
    /// Gets or sets the absolute address of the store's website.
    /// </summary>
    public string SellerUrl { get; set; }

    /// <summary>
    /// Gets or sets the absolute address of the store's terms of service, or <see langword="null"/>.
    /// </summary>
    public string TermsOfServiceUrl { get; set; }

    /// <summary>
    /// Gets or sets the absolute address of the store's privacy policy, or <see langword="null"/>.
    /// </summary>
    public string PrivacyPolicyUrl { get; set; }

    /// <summary>
    /// Gets or sets the absolute address of the store's return policy, or <see langword="null"/>.
    /// </summary>
    public string ReturnPolicyUrl { get; set; }

    /// <summary>
    /// Gets or sets what shipping one unit costs at the least, in <see cref="Currency"/>, or <see langword="null"/>
    /// when it isn't known.
    /// </summary>
    public decimal? ShippingPrice { get; set; }

    /// <summary>
    /// Gets or sets whether buyers can return the item, or <see langword="null"/> when it isn't known.
    /// </summary>
    public bool? AcceptsReturns { get; set; }

    /// <summary>
    /// Gets or sets for how many days after buying the item it can be returned, or <see langword="null"/>.
    /// </summary>
    public int? ReturnWindowDays { get; set; }

    /// <summary>
    /// Gets or sets whether buyers can exchange the item, or <see langword="null"/> when it isn't known.
    /// </summary>
    public bool? AcceptsExchanges { get; set; }

    /// <summary>
    /// Gets or sets the weight of one unit in <see cref="WeightUnit"/>, or <see langword="null"/>.
    /// </summary>
    public decimal? Weight { get; set; }

    /// <summary>
    /// Gets or sets the unit of <see cref="Weight"/>: <c>g</c>, <c>kg</c>, <c>oz</c> or <c>lb</c>.
    /// </summary>
    public string WeightUnit { get; set; }

    /// <summary>
    /// Gets or sets the length of one unit in <see cref="DimensionsUnit"/>, or <see langword="null"/>.
    /// </summary>
    public decimal? Length { get; set; }

    /// <summary>
    /// Gets or sets the width of one unit in <see cref="DimensionsUnit"/>, or <see langword="null"/>.
    /// </summary>
    public decimal? Width { get; set; }

    /// <summary>
    /// Gets or sets the height of one unit in <see cref="DimensionsUnit"/>, or <see langword="null"/>.
    /// </summary>
    public decimal? Height { get; set; }

    /// <summary>
    /// Gets or sets the unit of the dimensions: <c>in</c> or <c>cm</c>.
    /// </summary>
    public string DimensionsUnit { get; set; }

    /// <summary>
    /// Gets or sets whether the item is delivered digitally, such as a download.
    /// </summary>
    public bool IsDigital { get; set; }

    /// <summary>
    /// Gets or sets whether AI agents may show the item to buyers.
    /// </summary>
    public bool ShowToAgents { get; set; } = true;

    /// <summary>
    /// Gets or sets whether AI agents may check the item out for a buyer.
    /// </summary>
    public bool AgentCheckout { get; set; }

    /// <summary>
    /// Gets whether the product has a brand and a GTIN or MPN; when it doesn't, the feed says so with
    /// <c>identifier_exists</c>.
    /// </summary>
    public bool IdentifierExists
        => !string.IsNullOrWhiteSpace(Brand) && (!string.IsNullOrWhiteSpace(Gtin) || !string.IsNullOrWhiteSpace(Mpn));
}
