namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// The content-type-level settings for the <see cref="ProductPart"/>.
/// </summary>
public sealed class ProductPartSettings
{
    /// <summary>
    /// Gets or sets the product type applied to every item of the content type.
    /// </summary>
    public ProductType Type { get; set; }

    /// <summary>
    /// Gets or sets the default ISO-4217 currency code for products of the content type. It prefills the
    /// editor and is the currency a product is sold in when an item does not set its own
    /// <see cref="ProductPart.Currency"/>, so a price always has an owning currency.
    /// </summary>
    public string DefaultCurrency { get; set; }

    /// <summary>
    /// Gets or sets whether products of the content type can be bought online, are only listed, or decide per item.
    /// </summary>
    public ProductSalesMode SalesMode { get; set; }

    /// <summary>
    /// Gets or sets how the price of products of the content type is shown.
    /// </summary>
    public ProductPriceDisplay PriceDisplay { get; set; }

    /// <summary>
    /// Gets or sets the text shown instead of the price when the price is shown on request, such as
    /// "Call for price". A default label is used when it is empty.
    /// </summary>
    public string OnRequestLabel { get; set; }

    /// <summary>
    /// Gets or sets whether each product may choose how its own price is shown.
    /// </summary>
    public bool AllowPriceDisplayOverride { get; set; }
}
