using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Applies money the payment ledger confirmed to the transaction it was taken for. It is what the checkout runs when a
/// payment for a transaction completes, and what anything that charged a transaction runs to make sure that money is
/// applied before it considers charging again.
/// </summary>
public interface ITransactionSettlementService
{
    /// <summary>
    /// Applies every succeeded attempt in <paramref name="attempts"/> that is not on the transaction yet, saves the
    /// transaction, and tells the payment handlers. An attempt already applied is never counted again, and an attempt
    /// in another currency is refused rather than converted.
    /// </summary>
    /// <param name="transaction">The transaction the attempts paid for.</param>
    /// <param name="attempts">The attempts to consider; only succeeded ones are applied.</param>
    /// <param name="settlementReference">The reference recorded as how the transaction was settled, such as the checkout session.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when money was applied.</returns>
    Task<bool> ApplyAsync(Transaction transaction, IEnumerable<PaymentAttempt> attempts, string settlementReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies part of one succeeded attempt to the transaction, for a payment that settled several transactions at
    /// once: each transaction gets its share of the one payment. The share is recorded as a payment naming the attempt,
    /// so an attempt already applied to this transaction is never counted again, and refunds and receipts work per
    /// transaction as for a payment of one transaction.
    /// </summary>
    /// <param name="transaction">The transaction the share paid for.</param>
    /// <param name="attempt">The succeeded attempt the share was taken from.</param>
    /// <param name="amount">The share of the attempt's confirmed total, tax included, applied to this transaction. It can't be more than the attempt confirmed.</param>
    /// <param name="settlementReference">The reference recorded as how the transaction was settled, such as the checkout session.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when money was applied.</returns>
    Task<bool> ApplyShareAsync(Transaction transaction, PaymentAttempt attempt, decimal amount, string settlementReference, CancellationToken cancellationToken = default);
}
