namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// A read-only projection of a Stripe Checkout Session used to finalize a purchase after the
/// customer returns from a hosted checkout.
/// </summary>
public sealed class CheckoutSessionDetails
{
    public string Id { get; set; }

    /// <summary>
    /// The lifecycle status of the session (for example <c>complete</c>, <c>open</c> or <c>expired</c>).
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// The payment status (for example <c>paid</c>, <c>unpaid</c> or <c>no_payment_required</c>).
    /// </summary>
    public string PaymentStatus { get; set; }

    public string Mode { get; set; }

    public string CustomerId { get; set; }

    public string SubscriptionId { get; set; }

    public string Currency { get; set; }

    /// <summary>
    /// The total amount collected, expressed in the major currency unit (for example dollars, not cents).
    /// </summary>
    public double AmountTotal { get; set; }

    public bool Livemode { get; set; }

    /// <summary>
    /// Indicates the checkout finished and payment was collected (or was not required).
    /// </summary>
    public bool IsPaid
        => string.Equals(Status, "complete", StringComparison.OrdinalIgnoreCase) &&
           (string.Equals(PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(PaymentStatus, "no_payment_required", StringComparison.OrdinalIgnoreCase));
}
