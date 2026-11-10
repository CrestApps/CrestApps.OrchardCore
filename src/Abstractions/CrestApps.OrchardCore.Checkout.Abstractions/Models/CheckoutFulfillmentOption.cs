namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// One way to ship a checkout's order to an address.
/// </summary>
public sealed class CheckoutFulfillmentOption
{
    /// <summary>
    /// Gets or sets the option's id, which <see cref="CheckoutFulfillmentChoice.OptionId"/> names. It is the same for
    /// the same address and order every time it is quoted.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the option's name, such as "Standard".
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets a short description, such as how long it takes.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the carrier or the rate provider's name.
    /// </summary>
    public string Carrier { get; set; }

    /// <summary>
    /// Gets or sets what it costs, before tax.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency of <see cref="Amount"/>.
    /// </summary>
    public string Currency { get; set; }
}
