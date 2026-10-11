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
}
