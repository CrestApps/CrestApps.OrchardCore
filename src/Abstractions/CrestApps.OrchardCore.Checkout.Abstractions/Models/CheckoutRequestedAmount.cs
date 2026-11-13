namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// How much of an existing balance the buyer asked to pay in a checkout that settles one, kept in the session's property
/// bag from <see cref="Services.StartCheckoutRequest.Amount"/>. The module that owns the balance reads it when it builds
/// what the checkout charges, and decides whether a part payment of that amount is accepted.
/// </summary>
public sealed class CheckoutRequestedAmount
{
    /// <summary>
    /// Gets or sets the amount the buyer asked to pay, in the currency of the balance.
    /// </summary>
    public decimal Amount { get; set; }
}
