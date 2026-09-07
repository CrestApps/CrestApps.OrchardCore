namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Provides details about a failed recurring-cycle payment observed at a payment gateway, so a feature can
/// move the affected subscription into a past-due state, notify the customer, and start dunning.
/// </summary>
/// <remarks>
/// This is deliberately separate from <see cref="PaymentFailedContext"/>. A one-time payment failing means the
/// purchase did not happen; a <em>renewal</em> failing means an existing, previously paid subscription is now at
/// risk, which is a different decision (grace period, retries, revoke access) made by a different consumer.
/// The gateway remains authoritative: this notification reports what it observed and never itself cancels
/// anything.
/// </remarks>
public sealed class SubscriptionPaymentFailedContext : PaymentEventContextBase
{
    /// <summary>
    /// Gets or sets the gateway identifier of the subscription whose cycle payment failed.
    /// </summary>
    public string SubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets the gateway identifier of the invoice or transaction that failed, used to keep handling
    /// idempotent across the gateway's at-least-once delivery.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the amount the gateway attempted to collect, when it reports one.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency of the attempted collection.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the gateway failure code, when available.
    /// </summary>
    public string FailureCode { get; set; }

    /// <summary>
    /// Gets or sets the human-readable failure reason, when available.
    /// </summary>
    public string FailureReason { get; set; }

    /// <summary>
    /// Gets or sets how many collection attempts the gateway has made for this cycle, when it reports it.
    /// </summary>
    public int? AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of the gateway's next scheduled retry, when one is planned. A
    /// <see langword="null"/> value means the gateway has stopped retrying this cycle.
    /// </summary>
    public DateTime? NextAttemptUtc { get; set; }
}
