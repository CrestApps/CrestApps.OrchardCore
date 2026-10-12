namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents a request to retrieve the authoritative state of a Stripe PaymentIntent. Retrieval is how a
/// checkout verifies what really happened at the gateway instead of trusting a cached webhook notification.
/// </summary>
public sealed class RetrievePaymentIntentRequest
{
    /// <summary>
    /// Gets or sets the Stripe PaymentIntent identifier to retrieve.
    /// </summary>
    public string PaymentIntentId { get; set; }
}
