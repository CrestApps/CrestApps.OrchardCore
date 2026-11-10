namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// The outcome of charging a saved payment method without the customer present.
/// </summary>
public sealed class ChargeOffSessionResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether Stripe accepted the charge. The payment is still only treated as
    /// paid once it is verified against Stripe; this tells the caller whether there is anything to verify.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the PaymentIntent Stripe created, including for a declined charge when Stripe created one.
    /// </summary>
    public string PaymentIntentId { get; set; }

    /// <summary>
    /// Gets or sets the PaymentIntent status Stripe reported.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets Stripe's explanation of a refused charge, worded for the payer.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets Stripe's decline code (for example <c>insufficient_funds</c>), when the card was declined.
    /// </summary>
    public string DeclineCode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the bank asked for the customer to authenticate, which cannot
    /// happen without them present. The customer has to pay this one themselves.
    /// </summary>
    public bool RequiresAuthentication { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Stripe gave its final answer and took no money: a card error, a request
    /// it refused, or a PaymentIntent that ended without the money. Any other failure (a server error, a rate limit,
    /// an idempotency conflict) leaves the outcome unknown, and a retry must reuse the idempotency key so it never
    /// charges twice.
    /// </summary>
    public bool Declined { get; set; }
}
