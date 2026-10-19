using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Shipping.Models;

/// <summary>
/// What the buyer chose on the Shipping step: the address, from their address book or entered, and the rate.
/// </summary>
public sealed class ShippingCheckoutStep
{
    /// <summary>
    /// Gets or sets the id of the address-book entry shipped to, or <see langword="null"/> for an address entered.
    /// </summary>
    public string AddressId { get; set; }

    public Address Address { get; set; }

    public string RateId { get; set; }

    /// <summary>
    /// Gets or sets the store location the buyer picks the order up from, or <see langword="null"/> when it's shipped.
    /// </summary>
    public string PickupLocationId { get; set; }

    /// <summary>
    /// Gets or sets whether a signed-in buyer asked to keep the address they entered in their address book.
    /// </summary>
    public bool SaveAddress { get; set; }
}
