namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents the result returned after confirming a Stripe PaymentIntent.
/// </summary>
public class ConfirmPaymentIntentResponse
{
    /// <summary>
    /// Gets or sets the Stripe PaymentIntent identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the confirmed amount in major currency units, when available.
    /// </summary>
    public double? Amount { get; set; }

    /// <summary>
    /// Gets or sets the ISO currency code for the PaymentIntent.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the Stripe customer identifier associated with the PaymentIntent.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the Stripe status of the PaymentIntent.
    /// </summary>
    public string Status { get; set; }
}
