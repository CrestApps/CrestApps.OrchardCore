using System.Linq.Expressions;
using CrestApps.OrchardCore.Retention;
using YesSql;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.YesSql.Core.Retention;

/// <summary>
/// The retention policy a YesSql-stored record starts from: it reads expired records through an index a batch at a
/// time and removes them, so a policy only declares its window, its floor and what makes one of its records expired.
/// </summary>
/// <typeparam name="TModel">The stored record type.</typeparam>
/// <typeparam name="TIndex">The index whose table carries the age column.</typeparam>
public abstract class DocumentRetentionPolicyBase<TModel, TIndex> : IRetentionPolicy
    where TModel : class
    where TIndex : class, IIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentRetentionPolicyBase{TModel, TIndex}"/> class.
    /// </summary>
    /// <param name="session">The tenant session the expired records are read and removed through.</param>
    /// <param name="collectionName">The YesSql collection the records are stored in, or <see langword="null"/>.</param>
    protected DocumentRetentionPolicyBase(ISession session, string collectionName)
    {
        // A policy is also built without a session to inspect its cutoff and predicate, so none is required here.
        Session = session;
        CollectionName = collectionName;
    }

    /// <summary>
    /// Gets the tenant session.
    /// </summary>
    protected ISession Session { get; }

    /// <summary>
    /// Gets the YesSql collection the records are stored in.
    /// </summary>
    protected string CollectionName { get; }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the retention window in days; zero or less keeps the records forever.
    /// </summary>
    /// <returns>The window in days.</returns>
    protected abstract double GetRetentionDays();

    /// <summary>
    /// Gets a floor in days the window can never go below. The default is none.
    /// </summary>
    /// <returns>The floor in days.</returns>
    protected virtual double GetMinimumRetentionDays() => 0;

    /// <summary>
    /// Builds the predicate that selects the records old enough to remove. It must leave out records still in use.
    /// </summary>
    /// <param name="cutoffUtc">The exclusive UTC cutoff.</param>
    /// <returns>The predicate.</returns>
    protected abstract Expression<Func<TIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc);

    /// <summary>
    /// Runs before an expired record is removed: a policy can run deletion side effects or keep the record.
    /// </summary>
    /// <param name="record">The expired record.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> to remove the record; otherwise, <see langword="false"/>.</returns>
    protected virtual Task<bool> TryPrepareForDeletionAsync(TModel record, CancellationToken cancellationToken)
        => Task.FromResult(true);

    /// <summary>
    /// Removes one expired record. The default deletes it from the session; a policy whose records live in a catalog
    /// deletes them through it so the catalog's delete behavior still runs.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the record is removed.</returns>
    protected virtual ValueTask DeleteAsync(TModel record, CancellationToken cancellationToken)
    {
        Session.Delete(record, CollectionName);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual bool TryGetCutoff(DateTime nowUtc, out DateTime cutoffUtc)
        => RetentionCutoff.TryCompute(nowUtc, GetRetentionDays(), GetMinimumRetentionDays(), out cutoffUtc);

    /// <inheritdoc/>
    public async Task<int> PurgeBatchAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken = default)
    {
        var take = batchSize <= 0 ? 100 : batchSize;

        var expired = await Session
            .Query<TModel, TIndex>(BuildExpiredPredicate(cutoffUtc), collection: CollectionName)
            .Take(take)
            .ListAsync(cancellationToken);

        var purged = 0;

        foreach (var record in expired)
        {
            try
            {
                if (!await TryPrepareForDeletionAsync(record, cancellationToken))
                {
                    continue;
                }

                await DeleteAsync(record, cancellationToken);
            }
            catch (Exception ex)
            {
                // Earlier records of the batch are staged too, and the shared session cannot withdraw only some of
                // them, so the batch is reported as failed and the runner discards it whole.
                throw OnBatchFailed(purged, ex);
            }

            purged++;
        }

        return purged;
    }

    /// <summary>
    /// Creates the exception a failed batch is reported with.
    /// </summary>
    /// <param name="stagedCount">How many records the batch had staged.</param>
    /// <param name="exception">The failure.</param>
    /// <returns>The exception to throw.</returns>
    protected virtual Exception OnBatchFailed(int stagedCount, Exception exception)
        => new RetentionBatchException(Name, stagedCount, exception);
}
