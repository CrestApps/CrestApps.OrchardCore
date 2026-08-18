using CrestApps.Core.Models;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// The default YesSql-backed <see cref="ITransactionStore"/>. It persists the provider-agnostic
/// transaction ledger in the tenant database so outstanding obligations are never lost.
/// </summary>
public sealed class TransactionStore : DocumentCatalog<Transaction, TransactionIndex>, ITransactionStore
{
    private readonly IClock _clock;

    /// <summary>
    /// Enables YesSql document-version concurrency checks so two nodes settling or updating the same
    /// transaction can never silently overwrite each other; a conflicting write fails instead of losing an
    /// update.
    /// </summary>
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionStore"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session.</param>
    /// <param name="clock">The clock used for timestamps.</param>
    public TransactionStore(
        ISession session,
        IClock clock)
        : base(session)
    {
        CollectionName = TransactionsConstants.CollectionName;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<PageResult<Transaction>> PageAsync(int page, int pageSize, TransactionQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var records = Session.Query<Transaction, TransactionIndex>(collection: CollectionName);

        if (!string.IsNullOrEmpty(query.OwnerId))
        {
            records = records.Where(x => x.OwnerId == query.OwnerId);
        }

        if (query.OwnerKind.HasValue)
        {
            records = records.Where(x => x.OwnerKind == query.OwnerKind.Value);
        }

        if (!string.IsNullOrEmpty(query.Source))
        {
            records = records.Where(x => x.Source == query.Source);
        }

        if (!string.IsNullOrEmpty(query.ReferenceType))
        {
            records = records.Where(x => x.ReferenceType == query.ReferenceType);
        }

        if (!string.IsNullOrEmpty(query.ReferenceId))
        {
            records = records.Where(x => x.ReferenceId == query.ReferenceId);
        }

        if (query.OutstandingOnly)
        {
            records = records.Where(x => x.Status == TransactionStatus.Outstanding || x.Status == TransactionStatus.PartiallyPaid);
        }
        else if (query.Status.HasValue)
        {
            records = records.Where(x => x.Status == query.Status.Value);
        }

        if (!string.IsNullOrEmpty(query.Search))
        {
            records = records.Where(x => x.Title.Contains(query.Search));
        }

        var skip = (Math.Max(page, 1) - 1) * pageSize;

        var orderedRecords = records
            .OrderByDescending(x => x.CreatedUtc)
            .ThenByDescending(x => x.ItemId);

        return new PageResult<Transaction>
        {
            Count = await orderedRecords.CountAsync(cancellationToken),
            Entries = (await orderedRecords.Skip(skip).Take(pageSize).ListAsync(cancellationToken)).ToArray(),
        };
    }

    /// <inheritdoc/>
    public Task<Transaction> GetByObligationAsync(string checkoutSessionId, string obligationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(checkoutSessionId);
        ArgumentException.ThrowIfNullOrEmpty(obligationId);

        return Session.Query<Transaction, TransactionIndex>(
            x => x.CheckoutSessionId == checkoutSessionId && x.ObligationId == obligationId,
            collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Transaction>> GetOutstandingDueAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        var records = await Session.Query<Transaction, TransactionIndex>(
            x => (x.Status == TransactionStatus.Outstanding || x.Status == TransactionStatus.PartiallyPaid) &&
                (x.DueUtc == null || x.DueUtc <= asOfUtc),
            collection: CollectionName)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }

    /// <inheritdoc/>
    protected override ValueTask SavingAsync(Transaction record)
    {
        var now = _clock.UtcNow;

        if (record.CreatedUtc == default)
        {
            record.CreatedUtc = now;
        }

        record.UpdatedUtc = now;

        return ValueTask.CompletedTask;
    }
}
