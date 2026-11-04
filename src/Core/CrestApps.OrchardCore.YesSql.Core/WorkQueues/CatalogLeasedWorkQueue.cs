using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.WorkQueues;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.YesSql.Core.WorkQueues;

/// <summary>
/// The <see cref="ILeasedWorkQueue{T}"/> over a record's own catalog, committing every claim and settlement in the
/// caller's session. It suits a consumer that already runs each item in an isolated scope with a session of its own.
/// </summary>
/// <remarks>
/// The catalog must save with YesSql's concurrency check. A lost race surfaces as the <see cref="ConcurrencyException"/>
/// of the caller's session, which is then unusable, so the caller lets it propagate out of the item's scope and the
/// item is reclaimed once its lease expires.
/// </remarks>
/// <typeparam name="T">The work item type.</typeparam>
public sealed class CatalogLeasedWorkQueue<T> : LeasedWorkQueueBase<T>
    where T : CatalogItem, ILeasedWorkItem
{
    private readonly ICatalog<T> _catalog;
    private readonly ISession _session;
    private readonly Func<DateTime, int, CancellationToken, Task<IReadOnlyCollection<T>>> _listDue;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogLeasedWorkQueue{T}"/> class.
    /// </summary>
    /// <param name="catalog">The record's catalog.</param>
    /// <param name="session">The caller's session, committed after every claim and settlement.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="options">How the queue retires its items. The collection is the catalog's.</param>
    /// <param name="listDue">Lists the items due at a time, oldest first, up to a count.</param>
    public CatalogLeasedWorkQueue(
        ICatalog<T> catalog,
        ISession session,
        IClock clock,
        LeasedWorkQueueOptions options,
        Func<DateTime, int, CancellationToken, Task<IReadOnlyCollection<T>>> listDue)
        : base(clock, options)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(listDue);

        _catalog = catalog;
        _session = session;
        _listDue = listDue;
    }

    /// <inheritdoc/>
    protected override async Task AddAsync(T item, CancellationToken cancellationToken)
        => await _catalog.CreateAsync(item, cancellationToken);

    /// <inheritdoc/>
    protected override async Task<IReadOnlyList<string>> ListDueIdsAsync(DateTime nowUtc, int maxCount, CancellationToken cancellationToken)
    {
        var due = await _listDue(nowUtc, maxCount, cancellationToken);

        return due.Select(item => item.ItemId).ToArray();
    }

    /// <inheritdoc/>
    protected override async Task<T> CommitAsync(string itemId, Func<T, bool> change, bool remove, CancellationToken cancellationToken)
    {
        var item = await _catalog.FindByIdAsync(itemId, cancellationToken);

        if (item is null || !change(item))
        {
            return null;
        }

        if (remove)
        {
            await _catalog.DeleteAsync(item, cancellationToken);
        }
        else
        {
            await _catalog.UpdateAsync(item, cancellationToken);
        }

        await _session.SaveChangesAsync(cancellationToken);

        return item;
    }
}
