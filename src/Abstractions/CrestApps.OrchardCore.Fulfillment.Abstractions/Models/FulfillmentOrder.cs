namespace CrestApps.OrchardCore.Fulfillment.Models;

/// <summary>
/// A paid order as a fulfillment partner sees it: who it goes to and what to make. The engine builds it from the
/// store's order, so a provider never depends on the order model.
/// </summary>
public sealed class FulfillmentOrder
{
    /// <summary>
    /// Gets or sets the store's identifier of the order. Providers send it as their own external reference, so the
    /// same order is never created twice at the partner.
    /// </summary>
    public string OrderId { get; set; }

    /// <summary>
    /// Gets or sets the order number shown to people.
    /// </summary>
    public string OrderNumber { get; set; }

    /// <summary>
    /// Gets or sets who the parcel goes to.
    /// </summary>
    public FulfillmentRecipient Recipient { get; set; } = new();

    /// <summary>
    /// Gets the physical lines of the order. Lines that do not ship (downloads, services) are left out.
    /// </summary>
    public IList<FulfillmentOrderLine> Lines { get; } = [];
}
