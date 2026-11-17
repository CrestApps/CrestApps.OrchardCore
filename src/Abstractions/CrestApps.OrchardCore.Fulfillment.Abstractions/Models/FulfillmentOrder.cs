using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Fulfillment.Models;

/// <summary>
/// A paid order as a fulfillment partner sees it: where it goes and what to make. The engine builds it from the
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
    /// Gets or sets the address the parcel goes to, as the order holds it. <see cref="Address.Name"/> is the
    /// recipient's full name; <see cref="FulfillmentNames.Split(string)"/> splits it for a partner that wants a first and
    /// a last name.
    /// </summary>
    public Address ShippingAddress { get; set; }

    /// <summary>
    /// Gets or sets the buyer's email address.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the phone number to give the carrier: the shipping address's, or else the buyer's.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets the physical lines still to ship, with the quantities left: units already in a shipment are not here.
    /// When the engine calls <see cref="Services.IFulfillmentProvider.SubmitAsync"/>, only the lines assigned to that
    /// partner are here.
    /// </summary>
    public IList<FulfillmentOrderLine> Lines { get; } = [];
}
