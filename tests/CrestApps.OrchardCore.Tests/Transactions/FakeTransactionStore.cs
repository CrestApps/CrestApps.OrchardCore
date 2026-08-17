using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// An in-memory <see cref="ITransactionStore"/> that exercises the manager and handlers without a database.
/// </summary>
internal sealed class FakeTransactionStore : ITransactionStore
{
    private readonly Dictionary<string, Transaction> _transactions = new(StringComparer.Ordinal);

    public FakeTransactionStore(params Transaction[] seed)
    {
        foreach (var transaction in seed)
        {
            if (string.IsNullOrEmpty(transaction.ItemId))
            {
                transaction.ItemId = UniqueId.GenerateId();
            }

            _transactions[transaction.ItemId] = transaction;
        }
    }

    public IReadOnlyCollection<Transaction> Transactions
        => _transactions.Values;

    public ValueTask CreateAsync(Transaction entry, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(entry.ItemId))
        {
            entry.ItemId = UniqueId.GenerateId();
        }

        _transactions[entry.ItemId] = entry;

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(Transaction entry, CancellationToken cancellationToken = default)
    {
        _transactions[entry.ItemId] = entry;

        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> DeleteAsync(Transaction entry, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_transactions.Remove(entry.ItemId));

    public ValueTask<Transaction> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_transactions.GetValueOrDefault(id));

    public ValueTask<IReadOnlyCollection<Transaction>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<Transaction>>(_transactions.Values.Where(t => ids.Contains(t.ItemId, StringComparer.Ordinal)).ToArray());

    public ValueTask<IReadOnlyCollection<Transaction>> GetAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<Transaction>>(_transactions.Values.ToArray());

    public ValueTask<PageResult<Transaction>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
        where TQuery : QueryContext
    {
        var entries = _transactions.Values.ToArray();

        return ValueTask.FromResult(new PageResult<Transaction>
        {
            Count = entries.Length,
            Entries = entries,
        });
    }

    public Task<PageResult<Transaction>> PageAsync(int page, int pageSize, TransactionQuery query, CancellationToken cancellationToken = default)
    {
        var matches = _transactions.Values.Where(t => Matches(t, query))
            .OrderByDescending(t => t.CreatedUtc)
            .ToArray();

        var entries = matches
            .Skip(Math.Max(0, page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return Task.FromResult(new PageResult<Transaction>
        {
            Count = matches.Length,
            Entries = entries,
        });
    }

    public Task<Transaction> GetByObligationAsync(string checkoutSessionId, string obligationId, CancellationToken cancellationToken = default)
        => Task.FromResult(_transactions.Values.FirstOrDefault(t =>
            string.Equals(t.CheckoutSessionId, checkoutSessionId, StringComparison.Ordinal) &&
            string.Equals(t.ObligationId, obligationId, StringComparison.Ordinal)));

    public Task<IReadOnlyList<Transaction>> GetOutstandingDueAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        var results = _transactions.Values
            .Where(t =>
                (t.Status == TransactionStatus.Outstanding || t.Status == TransactionStatus.PartiallyPaid) &&
                (!t.DueUtc.HasValue || t.DueUtc.Value <= asOfUtc))
            .ToArray();

        return Task.FromResult<IReadOnlyList<Transaction>>(results);
    }

    private static bool Matches(Transaction transaction, TransactionQuery query)
    {
        if (!string.IsNullOrEmpty(query.OwnerId) && !string.Equals(transaction.OwnerId, query.OwnerId, StringComparison.Ordinal))
        {
            return false;
        }

        if (query.OutstandingOnly)
        {
            if (transaction.OutstandingAmount <= 0m)
            {
                return false;
            }
        }
        else if (query.Status.HasValue && transaction.Status != query.Status.Value)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(query.Source) && !string.Equals(transaction.Source, query.Source, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(query.ReferenceType) && !string.Equals(transaction.ReferenceType, query.ReferenceType, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(query.ReferenceId) && !string.Equals(transaction.ReferenceId, query.ReferenceId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(query.Search) &&
            (string.IsNullOrEmpty(transaction.Title) || transaction.Title.IndexOf(query.Search, StringComparison.OrdinalIgnoreCase) < 0))
        {
            return false;
        }

        return true;
    }
}
