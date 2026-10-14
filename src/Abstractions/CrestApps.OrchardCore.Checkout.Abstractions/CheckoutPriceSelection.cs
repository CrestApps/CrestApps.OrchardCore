namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// What the buyer chose from a product that is offered on more than one set of terms.
/// </summary>
/// <remarks>
/// It rides on the checkout session rather than being re-read from the request on every step, because a
/// checkout outlives the request that started it: the buyer can leave the payment page, come back, or have
/// their purchase completed by a provider notification, and they must be charged the terms they picked.
/// </remarks>
public sealed class CheckoutPriceSelection
{
    /// <summary>
    /// Gets or sets the identifier of the price the buyer chose.
    /// </summary>
    public string PriceId { get; set; }

    /// <summary>
    /// Gets or sets how many the buyer is taking.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the amount the buyer named, for a price that lets them name one.
    /// </summary>
    public decimal? CustomAmount { get; set; }
}
