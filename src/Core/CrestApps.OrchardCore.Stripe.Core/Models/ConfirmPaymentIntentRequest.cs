namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents a request to confirm an existing Stripe PaymentIntent.
/// </summary>
public class ConfirmPaymentIntentRequest : StripeWriteRequest
{
    /// <summary>
    /// Gets or sets the Stripe payment method identifier to attach during confirmation.
    /// </summary>
    public string PaymentMethodId { get; set; }

    /// <summary>
    /// Gets or sets the Stripe PaymentIntent identifier to confirm.
    /// </summary>
    public string PaymentIntentId { get; set; }
}
