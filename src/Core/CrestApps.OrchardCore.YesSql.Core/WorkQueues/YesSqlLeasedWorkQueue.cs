using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.WorkQueues;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.YesSql.Core.WorkQueues;

/// <summary>
/// The <see cref="ILeasedWorkQueue{T}"/> on YesSql that claims and settles in sessions of its own. A derived queue only
/// says how due items are found.
/// </summary>
/// <remarks>
/// <see cref="LeasedWorkQueueBase{T}.EnqueueAsync"/> saves into the caller's session. Every claim and settlement loads
/// the item in a fresh session, checks it, and saves it with YesSql's optimistic concurrency check, so two workers racing
/// for the same item cannot both win: the loser's save fails on the document version and it reports the claim as lost
/// without touching the caller's session. A transient SQLite lock is retried with a fresh session.
/// </remarks>
/// <typeparam name="T">The work item type.</typeparam>
/// <typeparam name="TIndex">The index with the item's <c>ItemId</c>, used to load it.</typeparam>
public abstract class YesSqlLeasedWorkQueue<T, TIndex> : LeasedWorkQueueBase<T>
    where T : CatalogItem, ILeasedWorkItem
    where TIndex : CatalogItemIndex
{
    private const int CommitAttempts = 3;

    private readonly IStore _store;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="YesSqlLeasedWorkQueue{T, TIndex}"/> class.
    /// </summary>
    /// <param name="store">The tenant store, which the claims and settlements open their own sessions on.</param>
    /// <param name="session">The caller's session, which new items are saved into.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="options">How the queue stores and retires its items.</param>
    protected YesSqlLeasedWorkQueue(IStore store, ISession session, IClock clock, LeasedWorkQueueOptions options)
        : base(clock, options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(session);

        _store = store;
        _session = session;
    }

    /// <summary>
    /// Returns the query of the items that are due at <paramref name="nowUtc"/>, oldest first: pending items whose retry
    /// time has come and claimed items whose lease expired.
    /// </summary>
    /// <param name="session">The session to query.</param>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <returns>The query of due items.</returns>
    protected abstract IQuery<T> QueryDue(ISession session, DateTime nowUtc);

    /// <inheritdoc/>
    protected override async Task AddAsync(T item, CancellationToken cancellationToken)
        => await _session.SaveAsync(item, checkConcurrency: true, collection: Options.CollectionName, cancellationToken: cancellationToken);

    /// <inheritdoc/>
    protected override async Task<IReadOnlyList<string>> ListDueIdsAsync(DateTime nowUtc, int maxCount, CancellationToken cancellationToken)
    {
        await using var session = _store.CreateSession();

        var due = await QueryDue(session, nowUtc)
            .Take(maxCount)
            .ListAsync(cancellationToken);

        return due.Select(item => item.ItemId).ToArray();
    }

    /// <inheritdoc/>
    protected override async Task<T> CommitAsync(string itemId, Func<T, bool> change, bool remove, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var session = _store.CreateSession();

                var item = await session.Query<T, TIndex>(index => index.ItemId == itemId, collection: Options.CollectionName)
                    .FirstOrDefaultAsync(cancellationToken);

                if (item is null || !change(item))
                {
                    return null;
                }

                if (remove)
                {
                    session.Delete(item, Options.CollectionName);
                }
                else
                {
                    await session.SaveAsync(item, checkConcurrency: true, collection: Options.CollectionName, cancellationToken: cancellationToken);
                }

                await session.SaveChangesAsync(cancellationToken);

                return item;
            }
            catch (ConcurrencyException)
            {
                // Another worker saved the item after it was loaded here, so it won the race.
                return null;
            }
            catch (Exception exception) when (attempt < CommitAttempts && IsTransientLock(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
            }
        }
    }

    private static bool IsTransientLock(Exception exception)
    {
        // SQLite reports a write lock held by another connection of the same process at once instead of waiting for
        // it. The fresh session of the next attempt makes the retry safe.
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;

            if (message is not null &&
                (message.Contains("database is locked", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("database table is locked", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("database is busy", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
