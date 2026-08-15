using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The input for <see cref="ICheckoutPaymentProvider.VerifyAsync"/>. Verification always queries the
/// provider's authoritative API rather than trusting a cached notification, which is what prevents the
/// checkout from marking an obligation paid when the gateway actually failed.
/// </summary>
public sealed class VerifyPaymentContext
{
    /// <summary>
    /// The checkout session the payment belongs to.
    /// </summary>
    public CheckoutSession Session { get; set; }

    /// <summary>
    /// The durable attempt being verified.
    /// </summary>
    public PaymentAttempt Attempt { get; set; }
}
