using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// An amount to take off what a transaction owes without money being received, to record with
/// <see cref="ITransactionCommandService.AdjustAsync(string, TransactionAdjustmentRequest, CancellationToken)"/>.
/// </summary>
public sealed class TransactionAdjustmentRequest
{
    /// <summary>
    /// Gets or sets the amount to take off, in the transaction's currency. It must be greater than zero.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets why the amount is taken off.
    /// </summary>
    public TransactionAdjustmentKind Kind { get; set; }

    /// <summary>
    /// Gets or sets a reason recorded with the adjustment.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the kind of document the adjustment is recorded for, such as <c>CreditNote</c>, when a document
    /// stands behind it.
    /// </summary>
    public string DocumentKind { get; set; }

    /// <summary>
    /// Gets or sets the identifier of that document. An adjustment already recorded for the same document is not
    /// recorded twice, so a caller can safely repeat the command after a failure.
    /// </summary>
    public string DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the number of that document as it is shown to people, such as <c>CN-1001</c>.
    /// </summary>
    public string DocumentNumber { get; set; }
}
