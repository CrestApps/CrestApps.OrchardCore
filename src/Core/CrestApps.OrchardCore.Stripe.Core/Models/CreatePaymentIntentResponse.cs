namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents the result returned after creating a Stripe PaymentIntent.
/// </summary>
public class CreatePaymentIntentResponse
{
    /// <summary>
    /// Gets or sets the Stripe PaymentIntent identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the client secret used by Stripe.js to complete the payment.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the Stripe customer identifier associated with the PaymentIntent.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the Stripe status of the PaymentIntent.
    /// </summary>
    public string Status { get; set; }
}
