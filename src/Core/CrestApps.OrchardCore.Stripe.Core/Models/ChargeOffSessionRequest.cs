namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents a request to charge a payment method a customer saved earlier, server-side and without the
/// customer present. The PaymentIntent is created and confirmed in one call with <c>off_session = true</c>.
/// </summary>
public sealed class ChargeOffSessionRequest : StripeWriteRequest
{
    /// <summary>
    /// Gets or sets the gross amount to charge in major currency units, including any tax.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency code.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the Stripe customer the saved payment method belongs to.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the saved Stripe payment method to charge.
    /// </summary>
    public string PaymentMethodId { get; set; }

    /// <summary>
    /// Gets or sets the optional description shown on the Stripe dashboard and receipts.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the metadata stored on the PaymentIntent, used to correlate it with the durable attempt.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}
