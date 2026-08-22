namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents a request to create and confirm a Stripe PaymentIntent.
/// </summary>
public class CreatePaymentIntentRequest : StripeWriteRequest
{
    /// <summary>
    /// Gets or sets the Stripe payment method identifier to use for the PaymentIntent.
    /// </summary>
    public string PaymentMethodId { get; set; }

    /// <summary>
    /// Gets or sets the Stripe customer identifier associated with the PaymentIntent.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the amount to charge in major currency units.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets the ISO currency code for the PaymentIntent.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the metadata to store on the Stripe PaymentIntent.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}
