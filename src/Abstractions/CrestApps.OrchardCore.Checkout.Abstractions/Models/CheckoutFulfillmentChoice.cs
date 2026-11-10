using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// How the buyer wants a checkout's order: shipped to an address with one of its options, or collected.
/// </summary>
public sealed class CheckoutFulfillmentChoice
{
    /// <summary>
    /// Gets or sets where to ship, when the order is shipped.
    /// </summary>
    public Address Address { get; set; }

    /// <summary>
    /// Gets or sets the chosen way to ship, from <see cref="CheckoutFulfillmentQuote.Options"/>.
    /// </summary>
    public string OptionId { get; set; }

    /// <summary>
    /// Gets or sets the place the buyer collects the order from, instead of shipping it.
    /// </summary>
    public string PickupLocationId { get; set; }
}
