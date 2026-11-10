using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// One product or variant in OpenAI's product feed for ChatGPT, with OpenAI's field names. A field without a value is
/// <see langword="null"/>, and is left out of a JSON line and empty in a CSV row.
/// </summary>
public sealed class OpenAIProductFeedRecord
{
    /// <summary>
    /// Gets or sets the item's id, stable and never reused: the id checkouts accept.
    /// </summary>
    [JsonPropertyName("item_id")]
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the item's title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the item's description, as plain text.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the address of the product's page.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the brand.
    /// </summary>
    [JsonPropertyName("brand")]
    public string Brand { get; set; }

    /// <summary>
    /// Gets or sets the store's name.
    /// </summary>
    [JsonPropertyName("seller_name")]
    public string SellerName { get; set; }

    /// <summary>
    /// Gets or sets the address of the item's main photo.
    /// </summary>
    [JsonPropertyName("image_url")]
    public string ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the addresses of the other photos.
    /// </summary>
    [JsonPropertyName("additional_image_urls")]
    public List<string> AdditionalImageUrls { get; set; }

    /// <summary>
    /// Gets or sets <c>in_stock</c>, <c>out_of_stock</c>, <c>pre_order</c>, <c>backorder</c> or <c>unknown</c>.
    /// </summary>
    [JsonPropertyName("availability")]
    public string Availability { get; set; }

    /// <summary>
    /// Gets or sets the price, such as <c>79.99 USD</c>.
    /// </summary>
    [JsonPropertyName("price")]
    public string Price { get; set; }

    /// <summary>
    /// Gets or sets the sale price, below the price and in its currency.
    /// </summary>
    [JsonPropertyName("sale_price")]
    public string SalePrice { get; set; }

    /// <summary>
    /// Gets or sets the id of the product a variant belongs to.
    /// </summary>
    [JsonPropertyName("group_id")]
    public string GroupId { get; set; }

    /// <summary>
    /// Gets or sets whether the item is a variant of a product with several.
    /// </summary>
    [JsonPropertyName("listing_has_variations")]
    public bool? ListingHasVariations { get; set; }

    /// <summary>
    /// Gets or sets a variant's options, such as <c>{"color":"Black","size":"10"}</c>.
    /// </summary>
    [JsonPropertyName("variant_dict")]
    public Dictionary<string, string> VariantDict { get; set; }

    /// <summary>
    /// Gets or sets the GTIN.
    /// </summary>
    [JsonPropertyName("gtin")]
    public string Gtin { get; set; }

    /// <summary>
    /// Gets or sets the maker's part number.
    /// </summary>
    [JsonPropertyName("mpn")]
    public string Mpn { get; set; }

    /// <summary>
    /// Gets or sets <c>new</c>, <c>refurbished</c> or <c>used</c>.
    /// </summary>
    [JsonPropertyName("condition")]
    public string Condition { get; set; }

    /// <summary>
    /// Gets or sets the category, such as <c>Apparel &gt; Shoes</c>.
    /// </summary>
    [JsonPropertyName("product_category")]
    public string ProductCategory { get; set; }

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    [JsonPropertyName("color")]
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets the size.
    /// </summary>
    [JsonPropertyName("size")]
    public string Size { get; set; }

    /// <summary>
    /// Gets or sets the weight of one unit, in <see cref="ItemWeightUnit"/>.
    /// </summary>
    [JsonPropertyName("weight")]
    public decimal? Weight { get; set; }

    /// <summary>
    /// Gets or sets <c>g</c>, <c>kg</c>, <c>oz</c> or <c>lb</c>.
    /// </summary>
    [JsonPropertyName("item_weight_unit")]
    public string ItemWeightUnit { get; set; }

    /// <summary>
    /// Gets or sets the length of one unit, in <see cref="DimensionsUnit"/>.
    /// </summary>
    [JsonPropertyName("length")]
    public decimal? Length { get; set; }

    /// <summary>
    /// Gets or sets the width of one unit, in <see cref="DimensionsUnit"/>.
    /// </summary>
    [JsonPropertyName("width")]
    public decimal? Width { get; set; }

    /// <summary>
    /// Gets or sets the height of one unit, in <see cref="DimensionsUnit"/>.
    /// </summary>
    [JsonPropertyName("height")]
    public decimal? Height { get; set; }

    /// <summary>
    /// Gets or sets <c>in</c> or <c>cm</c>.
    /// </summary>
    [JsonPropertyName("dimensions_unit")]
    public string DimensionsUnit { get; set; }

    /// <summary>
    /// Gets or sets what shipping one unit costs at the least, such as <c>4.99 USD</c>.
    /// </summary>
    [JsonPropertyName("shipping_price")]
    public string ShippingPrice { get; set; }

    /// <summary>
    /// Gets or sets whether the item can be returned.
    /// </summary>
    [JsonPropertyName("accepts_returns")]
    public bool? AcceptsReturns { get; set; }

    /// <summary>
    /// Gets or sets for how many days the item can be returned.
    /// </summary>
    [JsonPropertyName("return_deadline_in_days")]
    public int? ReturnDeadlineInDays { get; set; }

    /// <summary>
    /// Gets or sets the address of the return policy.
    /// </summary>
    [JsonPropertyName("return_policy")]
    public string ReturnPolicy { get; set; }

    /// <summary>
    /// Gets or sets whether the item can be exchanged.
    /// </summary>
    [JsonPropertyName("accepts_exchanges")]
    public bool? AcceptsExchanges { get; set; }

    /// <summary>
    /// Gets or sets the address of the store's website.
    /// </summary>
    [JsonPropertyName("seller_url")]
    public string SellerUrl { get; set; }

    /// <summary>
    /// Gets or sets the address of the store's privacy policy.
    /// </summary>
    [JsonPropertyName("seller_privacy_policy")]
    public string SellerPrivacyPolicy { get; set; }

    /// <summary>
    /// Gets or sets the address of the store's terms of service.
    /// </summary>
    [JsonPropertyName("seller_tos")]
    public string SellerTos { get; set; }

    /// <summary>
    /// Gets or sets whether the item is delivered digitally.
    /// </summary>
    [JsonPropertyName("is_digital")]
    public bool? IsDigital { get; set; }

    /// <summary>
    /// Gets or sets whether ChatGPT may show the item.
    /// </summary>
    [JsonPropertyName("is_eligible_search")]
    public bool? IsEligibleSearch { get; set; }

    /// <summary>
    /// Gets or sets whether ChatGPT may check the item out.
    /// </summary>
    [JsonPropertyName("is_eligible_checkout")]
    public bool? IsEligibleCheckout { get; set; }

    /// <summary>
    /// Gets or sets the two-letter code of the store's country.
    /// </summary>
    [JsonPropertyName("store_country")]
    public string StoreCountry { get; set; }

    /// <summary>
    /// Gets or sets the two-letter codes of the countries the item is sold to.
    /// </summary>
    [JsonPropertyName("target_countries")]
    public List<string> TargetCountries { get; set; }
}
