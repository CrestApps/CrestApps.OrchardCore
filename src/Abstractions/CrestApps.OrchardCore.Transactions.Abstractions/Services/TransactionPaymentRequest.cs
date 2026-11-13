namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Money received outside the site, to record against a transaction with
/// <see cref="ITransactionCommandService.RecordPaymentAsync"/>.
/// </summary>
public sealed class TransactionPaymentRequest
{
    /// <summary>
    /// Gets or sets the amount received, in the transaction's currency. It must be greater than zero.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets how the money was received, as a short key such as <c>cash</c>, <c>check</c> or
    /// <c>bank-transfer</c>. The caller chooses the keys it offers; the ledger stores the key as given.
    /// </summary>
    public string PaymentMethod { get; set; }

    /// <summary>
    /// Gets or sets when the money was received, in UTC, when that is not now: a check dated last week, for example.
    /// <see langword="null"/> records it as received now.
    /// </summary>
    public DateTime? ReceivedUtc { get; set; }

    /// <summary>
    /// Gets or sets a reference for the payment, such as a check number.
    /// </summary>
    public string Reference { get; set; }

    /// <summary>
    /// Gets or sets a note recorded with the payment.
    /// </summary>
    public string Note { get; set; }
}
