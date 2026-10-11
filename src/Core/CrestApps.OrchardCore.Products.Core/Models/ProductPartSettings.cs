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
}
