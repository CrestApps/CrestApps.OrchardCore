using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The input for <see cref="ICheckoutPaymentProvider.CancelAsync"/>. Cancellation releases or voids any
/// remote resource the provider created for an attempt that is being abandoned or compensated.
/// </summary>
public sealed class CancelPaymentContext
{
    /// <summary>
    /// The checkout session the payment belongs to.
    /// </summary>
    public CheckoutSession Session { get; set; }

    /// <summary>
    /// The durable attempt to cancel or compensate.
    /// </summary>
    public PaymentAttempt Attempt { get; set; }

    /// <summary>
    /// The reason the attempt is being canceled, recorded for audit.
    /// </summary>
    public string Reason { get; set; }
}
