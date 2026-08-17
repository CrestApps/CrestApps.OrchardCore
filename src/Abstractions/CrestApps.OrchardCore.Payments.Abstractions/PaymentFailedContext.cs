namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Provides details about a payment that failed at a payment gateway, so features can release
/// reservations, mark obligations unpaid, and notify the customer. A gateway failure notification is a
/// hint: the provider API remains authoritative when a webhook and local state disagree.
/// </summary>
public sealed class PaymentFailedContext : PaymentEventContextBase
{
    /// <summary>
    /// Gets or sets the payment gateway transaction identifier for the failed payment.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the amount that was attempted, when the gateway provides it.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency used by the failed payment.
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
}
