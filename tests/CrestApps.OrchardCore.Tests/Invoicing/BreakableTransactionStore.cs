using CrestApps.Core.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// The real transaction store, which a test can make fail on the next transaction it is asked to create: the step of issue
/// that posts what an invoice owes, after the invoice took its number.
/// </summary>
internal sealed class BreakableTransactionStore : ITransactionStore
{
    private readonly ITransactionStore _inner;
    private readonly Func<bool> _failNext;

    public BreakableTransactionStore(ITransactionStore inner, Func<bool> failNext)
    {
        _inner = inner;
        _failNext = failNext;
    }

    public ValueTask CreateAsync(Transaction entry, CancellationToken cancellationToken = default)
        => _failNext()
            ? throw new InvalidOperationException("The transaction could not be saved.")
            : _inner.CreateAsync(entry, cancellationToken);

    public ValueTask UpdateAsync(Transaction entry, CancellationToken cancellationToken = default)
        => _inner.UpdateAsync(entry, cancellationToken);

    public ValueTask<bool> DeleteAsync(Transaction entry, CancellationToken cancellationToken = default)
        => _inner.DeleteAsync(entry, cancellationToken);

    public ValueTask<Transaction> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => _inner.FindByIdAsync(id, cancellationToken);

    public ValueTask<IReadOnlyCollection<Transaction>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
        => _inner.GetAsync(ids, cancellationToken);

    public ValueTask<IReadOnlyCollection<Transaction>> GetAllAsync(CancellationToken cancellationToken = default)
        => _inner.GetAllAsync(cancellationToken);

    public ValueTask<PageResult<Transaction>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
        where TQuery : QueryContext
        => _inner.PageAsync(page, pageSize, context, cancellationToken);

    public Task<PageResult<Transaction>> PageAsync(int page, int pageSize, TransactionQuery query, CancellationToken cancellationToken = default)
        => _inner.PageAsync(page, pageSize, query, cancellationToken);

    public Task<Transaction> GetByObligationAsync(string checkoutSessionId, string obligationId, CancellationToken cancellationToken = default)
        => _inner.GetByObligationAsync(checkoutSessionId, obligationId, cancellationToken);

    public Task<IReadOnlyList<Transaction>> GetByParentReferenceAsync(string parentReferenceType, string parentReferenceId, CancellationToken cancellationToken = default)
        => _inner.GetByParentReferenceAsync(parentReferenceType, parentReferenceId, cancellationToken);

    public Task<IReadOnlyList<Transaction>> GetByCheckoutSessionAsync(string checkoutSessionId, CancellationToken cancellationToken = default)
        => _inner.GetByCheckoutSessionAsync(checkoutSessionId, cancellationToken);

    public Task<IReadOnlyList<Transaction>> GetOutstandingDueAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
        => _inner.GetOutstandingDueAsync(asOfUtc, cancellationToken);

    public Task<IReadOnlyList<Transaction>> GetComingDueAsync(DateTime afterUtc, DateTime untilUtc, CancellationToken cancellationToken = default)
        => _inner.GetComingDueAsync(afterUtc, untilUtc, cancellationToken);

    public Task<IReadOnlyList<Transaction>> GetDueForRenewalAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
        => _inner.GetDueForRenewalAsync(asOfUtc, cancellationToken);
}
