namespace CrestApps.OrchardCore.Fulfillment.Models;

/// <summary>
/// One physical line of an order sent to a fulfillment partner.
/// </summary>
public sealed class FulfillmentOrderLine
{
    /// <summary>
    /// Gets or sets the store's identifier of the order line.
    /// </summary>
    public string LineId { get; set; }

    /// <summary>
    /// Gets or sets the content item identifier of the product bought.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the store's identifier of the variant bought, or <see langword="null"/> for a product without
    /// variants.
    /// </summary>
    public string VariantId { get; set; }

    /// <summary>
    /// Gets or sets the store's SKU of what was bought.
    /// </summary>
    public string Sku { get; set; }

    /// <summary>
    /// Gets or sets the product title and, for a variant, its options, as the order shows them.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets how many were bought.
    /// </summary>
    public int Quantity { get; set; }
}
