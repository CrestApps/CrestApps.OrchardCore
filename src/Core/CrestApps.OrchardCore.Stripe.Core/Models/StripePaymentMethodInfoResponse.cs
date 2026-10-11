namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents Stripe payment method details returned to the application.
/// </summary>
public sealed class StripePaymentMethodInfoResponse
{
    /// <summary>
    /// Gets or sets the card details when the payment method is a card.
    /// </summary>
    public StripePaymentCardInfoResponse Card { get; set; }
}
