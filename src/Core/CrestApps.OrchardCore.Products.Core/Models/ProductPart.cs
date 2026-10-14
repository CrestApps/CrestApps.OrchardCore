using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// Represents product pricing data attached to an Orchard Core content item. This part is owned by the
/// Products domain; payment and checkout consume the resolved sellable snapshot rather than this part
/// directly.
/// </summary>
public sealed class ProductPart : ContentPart
{
    /// <summary>
    /// Gets or sets the product price. It is empty for a listed product whose price is not shown, such as a
    /// property sold by inquiry. A product without a price cannot be bought.
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency code the <see cref="Price"/> is expressed in. The product owns
    /// its currency so a price is never ambiguous; a checkout that requests a different currency is
    /// rejected rather than silently converted. When empty, the content type's default currency (from
    /// <see cref="ProductPartSettings.DefaultCurrency"/>) applies.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the stock-keeping unit that uniquely identifies the product for selling and
    /// fulfillment. Optional; a future catalog can require it per content type.
    /// </summary>
    public string Sku { get; set; }

    /// <summary>
    /// Gets or sets the "was" or list price shown struck through next to the price, from which the discount
    /// badge is worked out. It is shown only when it is higher than the price.
    /// </summary>
    public decimal? CompareAtPrice { get; set; }

    /// <summary>
    /// Gets or sets whether this product can be bought online. It is honored only when the content type's
    /// <see cref="ProductPartSettings.SalesMode"/> is <see cref="ProductSalesMode.PerItem"/>.
    /// </summary>
    public bool IsPurchasable { get; set; } = true;

    /// <summary>
    /// Gets or sets how this product's price is shown, overriding the content type's
    /// <see cref="ProductPartSettings.PriceDisplay"/>. It is honored only when the content type allows it.
    /// </summary>
    public ProductPriceDisplay? PriceDisplay { get; set; }
}
