namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// What a checkout pays for, beyond its own <see cref="CheckoutSession.ReferenceType"/> and
/// <see cref="CheckoutSession.OwnerId"/>, so a coupon limited to a source, a kind of charge or a customer can be
/// checked against it. The module that knows what the checkout pays for stores it in the session's property bag, for
/// example the Transactions module when a checkout settles a transaction: the transaction's source, what it is for, and
/// who owes it.
/// </summary>
public sealed class CheckoutDiscountScope
{
    /// <summary>
    /// Gets or sets where the charge being paid came from, such as the source of the transaction a checkout settles.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the kind of thing the charge is for, such as the reference type of the transaction a checkout
    /// settles.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the customer who owes the charge: a user id, or a guest customer id.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the email address of the customer who owes the charge, when it is known.
    /// </summary>
    public string CustomerEmail { get; set; }
}
