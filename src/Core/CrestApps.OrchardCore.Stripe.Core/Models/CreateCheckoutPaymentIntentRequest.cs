namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents a request to create an <em>unconfirmed</em> Stripe PaymentIntent for a generic checkout
/// payment collected through embedded Stripe Elements. Unlike <see cref="CreatePaymentIntentRequest"/>,
/// which creates and immediately confirms an off-session charge, this request returns a client secret the
/// browser uses to confirm the payment, so it never bypasses Strong Customer Authentication.
/// </summary>
public sealed class CreateCheckoutPaymentIntentRequest : StripeWriteRequest
{
    /// <summary>
    /// Gets or sets the gross amount to charge in major currency units, including any tax.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency code for the PaymentIntent.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the optional Stripe customer identifier associated with the PaymentIntent.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the optional description shown on the Stripe dashboard and receipts.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the metadata to store on the Stripe PaymentIntent, used to correlate it with the
    /// durable checkout attempt.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}
