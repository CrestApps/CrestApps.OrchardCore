namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// The ways a checkout's order can be shipped to an address.
/// </summary>
public sealed class CheckoutFulfillmentQuote
{
    /// <summary>
    /// Gets or sets a value indicating whether the order needs shipping at all.
    /// </summary>
    public bool RequiresShipping { get; set; } = true;

    /// <summary>
    /// Gets or sets the ways to ship, cheapest first; empty when nothing ships to the address.
    /// </summary>
    public IReadOnlyList<CheckoutFulfillmentOption> Options { get; set; } = [];

    /// <summary>
    /// Gets or sets why nothing ships to the address, or <see langword="null"/>.
    /// </summary>
    public string Problem { get; set; }
}
