namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents a request to cancel a Stripe PaymentIntent that has not yet been captured, used to release a
/// remote resource for a checkout attempt that is being abandoned or compensated.
/// </summary>
public sealed class CancelPaymentIntentRequest : StripeWriteRequest
{
    /// <summary>
    /// Gets or sets the Stripe PaymentIntent identifier to cancel.
    /// </summary>
    public string PaymentIntentId { get; set; }

    /// <summary>
    /// Gets or sets the optional Stripe cancellation reason (for example <c>abandoned</c>).
    /// </summary>
    public string CancellationReason { get; set; }
}
