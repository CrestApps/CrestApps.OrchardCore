namespace CrestApps.OrchardCore.Fulfillment.Core.Models;

/// <summary>
/// An order line assigned to a fulfillment partner.
/// </summary>
public sealed class FulfillmentRequestLine
{
    /// <summary>
    /// Gets or sets the store's identifier of the order line.
    /// </summary>
    public string LineId { get; set; }

    /// <summary>
    /// Gets or sets the line's title, with its options.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the store's SKU.
    /// </summary>
    public string Sku { get; set; }

    /// <summary>
    /// Gets or sets how many units the partner makes.
    /// </summary>
    public int Quantity { get; set; }
}
