namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// The authoritative details of a Stripe refund as reported by the Stripe API.
/// </summary>
public sealed class RefundDetails
{
    /// <summary>
    /// Gets or sets the Stripe refund identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the Stripe status of the refund (for example <c>succeeded</c>, <c>pending</c>, or <c>failed</c>).
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the refunded amount, in the currency's minor units.
    /// </summary>
    public long Amount { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency code of the refund.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the Stripe failure reason, when the refund failed.
    /// </summary>
    public string FailureReason { get; set; }
}
