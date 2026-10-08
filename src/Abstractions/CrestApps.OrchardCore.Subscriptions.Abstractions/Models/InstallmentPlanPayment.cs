namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// One payment on an <see cref="InstallmentPlan"/>. The money itself is recorded on the ledger transaction named by
/// <see cref="TransactionId"/>; this keeps where the payment stands and the history of trying to collect it.
/// </summary>
public sealed class InstallmentPlanPayment
{
    /// <summary>
    /// Gets or sets the position in the schedule: 0 for the down payment, then 1, 2, and so on.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets when the payment falls due.
    /// </summary>
    public DateTime DueUtc { get; set; }

    /// <summary>
    /// Gets or sets the amount of the payment.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the ledger transaction that records the payment.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets where the payment stands.
    /// </summary>
    public InstallmentPaymentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the payment was received.
    /// </summary>
    public DateTime? PaidUtc { get; set; }

    /// <summary>
    /// Gets or sets how many times the saved payment method was charged for this payment.
    /// </summary>
    public int ChargeAttempts { get; set; }

    /// <summary>
    /// Gets or sets when the saved payment method was last charged for this payment.
    /// </summary>
    public DateTime? LastChargeAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets when the saved payment method is charged again after a failed charge, or
    /// <see langword="null"/> when no further automatic charge is planned.
    /// </summary>
    public DateTime? NextChargeAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets why the last charge failed, as the payment provider explained it.
    /// </summary>
    public string LastFailureMessage { get; set; }
}
