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
    /// Gets or sets the product price.
    /// </summary>
    public decimal Price { get; set; }

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
}
