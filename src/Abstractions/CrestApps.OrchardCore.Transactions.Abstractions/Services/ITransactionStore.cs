using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The durable store for <see cref="Transaction"/> ledger entries. It is backed by the tenant database so
/// outstanding obligations survive cache eviction and node failure, and it exposes the queries needed to
/// build customer statements, administrator reports, and the reminder sweep.
/// </summary>
public interface ITransactionStore : ICatalog<Transaction>
{
    /// <summary>
    /// Returns a page of transactions that match the supplied <paramref name="query"/>, most recent first.
    /// </summary>
    /// <param name="page">The one-based page number to load.</param>
    /// <param name="pageSize">The number of entries per page.</param>
    /// <param name="query">The filter to apply.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PageResult<Transaction>> PageAsync(int page, int pageSize, TransactionQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the transaction created for the supplied checkout obligation, or <see langword="null"/> when
    /// none exists. Used to keep transaction creation idempotent when a checkout completes more than once.
    /// </summary>
    /// <param name="checkoutSessionId">The originating checkout session id.</param>
    /// <param name="obligationId">The obligation id within the checkout.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<Transaction> GetByObligationAsync(string checkoutSessionId, string obligationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the outstanding transactions (status <see cref="TransactionStatus.Outstanding"/> or
    /// <see cref="TransactionStatus.PartiallyPaid"/>) whose due date is on or before
    /// <paramref name="asOfUtc"/>, or that have no due date, so the reminder sweep can chase them.
    /// </summary>
    /// <param name="asOfUtc">The upper bound for the due date.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<Transaction>> GetOutstandingDueAsync(DateTime asOfUtc, CancellationToken cancellationToken = default);
}
