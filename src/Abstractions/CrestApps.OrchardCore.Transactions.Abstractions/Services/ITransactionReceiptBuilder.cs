using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Builds the receipt for one payment applied to a transaction.
/// </summary>
/// <remarks>
/// The Transactions module registers the default implementation whenever the Receipts module is enabled too, so
/// resolve it as optional: without the Receipts module there is no receipt to build.
/// </remarks>
public interface ITransactionReceiptBuilder
{
    /// <summary>
    /// Builds the receipt for the payment recorded by <paramref name="payment"/>, or returns <see langword="null"/>
    /// when the event did not record money.
    /// </summary>
    /// <param name="transaction">The transaction the payment was applied to.</param>
    /// <param name="payment">The timeline event that recorded the payment.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<ReceiptDocument> BuildAsync(Transaction transaction, TransactionEvent payment, CancellationToken cancellationToken = default);
}
