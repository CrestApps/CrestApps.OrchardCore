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
    /// Gets or sets the price the product sells at during its sale, between <see cref="SaleStartsUtc"/> and
    /// <see cref="SaleEndsUtc"/>. Outside it, or when it isn't lower than the price, the price applies.
    /// </summary>
    public decimal? SalePrice { get; set; }

    /// <summary>
    /// Gets or sets when the sale starts, or <see langword="null"/> for a sale that has started.
    /// </summary>
    public DateTime? SaleStartsUtc { get; set; }

    /// <summary>
    /// Gets or sets when the sale ends, or <see langword="null"/> for a sale with no end.
    /// </summary>
    public DateTime? SaleEndsUtc { get; set; }

    /// <summary>
    /// Gets or sets the fewest units a cart can hold of the product, or <see langword="null"/> for one.
    /// </summary>
    public int? MinQuantity { get; set; }

    /// <summary>
    /// Gets or sets where an external product is bought, such as a partner's store. A product with one isn't sold
    /// here: its card and page link there instead of offering Add to cart.
    /// </summary>
    public string ExternalUrl { get; set; }

    /// <summary>
    /// Gets or sets the label of the link to <see cref="ExternalUrl"/>, or <see langword="null"/> for "Buy now".
    /// </summary>
    public string ExternalButtonText { get; set; }

    /// <summary>
    /// Gets or sets the most units a cart can hold of the product, or <see langword="null"/> for the store's limit.
    /// </summary>
    public int? MaxQuantity { get; set; }

    /// <summary>
    /// Gets or sets the pack size the product is sold in: the quantity has to be a multiple of it.
    /// </summary>
    public int? QuantityStep { get; set; }

    /// <summary>
    /// Gets or sets the volume discounts: from a quantity on, each unit costs a percentage less.
    /// </summary>
    public List<ProductPriceTier> PriceTiers { get; set; } = [];

    /// <summary>
    /// Gets or sets the discounts buyers in a role get, such as a wholesale price for the Wholesale role.
    /// </summary>
    public List<ProductRoleDiscount> RoleDiscounts { get; set; } = [];

    /// <summary>
    /// Gets or sets the personal touches a buyer can add, such as an engraving or gift wrap, each at an extra price.
    /// </summary>
    public List<ProductAddOn> AddOns { get; set; } = [];

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

    /// <summary>
    /// Gets or sets whether the merchant features this product. Listings can sort featured products first and
    /// filter on them.
    /// </summary>
    public bool Featured { get; set; }

    /// <summary>
    /// Gets or sets the product's place in the order the merchant arranged: a smaller number comes first in a
    /// listing's "Recommended" sort, and products without one come after those with one.
    /// </summary>
    public int? Position { get; set; }
}
