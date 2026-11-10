using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// A place the buyer can collect a checkout's order from.
/// </summary>
public sealed class CheckoutPickupOption
{
    /// <summary>
    /// Gets or sets the location's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the location's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets where it is.
    /// </summary>
    public Address Address { get; set; }

    /// <summary>
    /// Gets or sets what to do when collecting.
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets the fee for collecting there.
    /// </summary>
    public decimal Fee { get; set; }

    /// <summary>
    /// Gets or sets the currency of <see cref="Fee"/>.
    /// </summary>
    public string Currency { get; set; }
}
