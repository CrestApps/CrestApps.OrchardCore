using System.Linq.Expressions;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// The default YesSql-backed <see cref="ITransactionStore"/>. It persists the provider-agnostic
/// transaction ledger in the tenant database so outstanding obligations are never lost.
/// </summary>
public sealed class TransactionStore : DocumentCatalog<Transaction, TransactionIndex>, ITransactionStore
{
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
        : base(session, clock)
    {
        CollectionName = TransactionsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<PageResult<Transaction>> PageAsync(int page, int pageSize, TransactionQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var records = Session.Query<Transaction, TransactionIndex>(collection: CollectionName);

        var payers = (query.Payers ?? []).Where(payer => !payer.IsEmpty).Distinct().ToArray();

        if (payers.Length > 0)
        {
            // The owner's own transactions and those the named parties owe: what a person sees when they also act for
            // an account.
            records = records.Where(BuildOwnerOrPayerPredicate(query.OwnerId, query.OwnerKind, payers));
        }
        else
        {
            if (!string.IsNullOrEmpty(query.OwnerId))
            {
                records = records.Where(x => x.OwnerId == query.OwnerId);
            }

            if (query.OwnerKind.HasValue)
            {
                records = records.Where(x => x.OwnerKind == query.OwnerKind.Value);
            }
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

        if (!string.IsNullOrEmpty(query.CheckoutSessionId))
        {
            records = records.Where(x => x.CheckoutSessionId == query.CheckoutSessionId);
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

    private static Expression<Func<TransactionIndex, bool>> BuildOwnerOrPayerPredicate(string ownerId, CustomerOwnerKind? ownerKind, PartyRef[] payers)
    {
        Expression<Func<TransactionIndex, bool>> predicate = null;

        if (!string.IsNullOrEmpty(ownerId))
        {
            if (ownerKind.HasValue)
            {
                var kind = ownerKind.Value;

                predicate = x => x.OwnerId == ownerId && x.OwnerKind == kind;
            }
            else
            {
                predicate = x => x.OwnerId == ownerId;
            }
        }

        foreach (var group in payers.GroupBy(payer => payer.Kind, StringComparer.Ordinal))
        {
            var payerKind = group.Key;
            var payerIds = group
                .Select(payer => TransactionIndexProvider.TruncateParentReference(payer.Id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            Expression<Func<TransactionIndex, bool>> owedByKind = x => x.PayerKind == payerKind && x.PayerId.IsIn(payerIds);

            predicate = IndexPredicates.OrElse(predicate, owedByKind);
        }

        return predicate;
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
    public async Task<IReadOnlyList<Transaction>> GetByCheckoutSessionAsync(string checkoutSessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(checkoutSessionId);

        // One checkout pays a handful of debts at most.
        var records = await Session.Query<Transaction, TransactionIndex>(
            x => x.CheckoutSessionId == checkoutSessionId,
            collection: CollectionName)
            .OrderBy(x => x.CreatedUtc)
            .Take(50)
            .ListAsync(cancellationToken);

        return [.. records];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Transaction>> GetByParentReferenceAsync(string parentReferenceType, string parentReferenceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(parentReferenceType);
        ArgumentException.ThrowIfNullOrEmpty(parentReferenceId);

        var type = TransactionIndexProvider.TruncateParentReference(parentReferenceType);
        var id = TransactionIndexProvider.TruncateParentReference(parentReferenceId);

        var records = await Session.Query<Transaction, TransactionIndex>(
            x => x.ParentReferenceType == type && x.ParentReferenceId == id,
            collection: CollectionName)
            .ListAsync(cancellationToken);

        // The index row is truncated; the document is not, so a longer reference is matched exactly here.
        return records
            .Where(transaction =>
                string.Equals(transaction.ParentReferenceType, parentReferenceType, StringComparison.Ordinal) &&
                string.Equals(transaction.ParentReferenceId, parentReferenceId, StringComparison.Ordinal))
            .OrderBy(transaction => transaction.CreatedUtc)
            .ThenBy(transaction => transaction.DueUtc)
            .ToArray();
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
    public async Task<IReadOnlyList<Transaction>> GetComingDueAsync(DateTime afterUtc, DateTime untilUtc, CancellationToken cancellationToken = default)
    {
        var records = await Session.Query<Transaction, TransactionIndex>(
            x => (x.Status == TransactionStatus.Pending || x.Status == TransactionStatus.Outstanding || x.Status == TransactionStatus.PartiallyPaid) &&
                x.DueUtc != null && x.DueUtc > afterUtc && x.DueUtc <= untilUtc,
            collection: CollectionName)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Transaction>> GetDueForRenewalAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        // A canceled agreement and one whose next cycle already exists both map NextCycleUtc to null, so the
        // column alone decides what is due. A transaction the customer never paid still renews: falling
        // behind on one invoice does not end the agreement, and the ledger is what shows the growing debt.
        var records = await Session.Query<Transaction, TransactionIndex>(
            x => x.NextCycleUtc != null && x.NextCycleUtc <= asOfUtc,
            collection: CollectionName)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }
}
