using System.Linq.Expressions;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Provides the shared batching, cutoff, and deletion behavior for a document-backed retention policy so each
/// entity only has to declare its window, its floors, and what makes one of its records expired.
/// </summary>
/// <typeparam name="TModel">The catalog item type being purged.</typeparam>
/// <typeparam name="TIndex">The index type whose table carries the age column.</typeparam>
public abstract class ContactCenterRetentionPolicyBase<TModel, TIndex> : IContactCenterRetentionPolicy
    where TModel : CatalogItem
    where TIndex : CatalogItemIndex
{
    private readonly ISession _session;
    private readonly ICatalog<TModel> _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterRetentionPolicyBase{TModel, TIndex}"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="catalog">
    /// The catalog that owns the records, used to delete them so any catalog-level delete behavior still runs
    /// instead of being bypassed by a raw session delete.
    /// </param>
    protected ContactCenterRetentionPolicyBase(
        ISession session,
        ICatalog<TModel> catalog)
    {
        _session = session;
        _catalog = catalog;
    }

    /// <inheritdoc/>
    public abstract string EntityName { get; }

    /// <inheritdoc/>
    public Type IndexType => typeof(TIndex);

    /// <inheritdoc/>
    public Type ModelType => typeof(TModel);

    /// <summary>
    /// Gets a value indicating whether this entity is held by the legal-hold floor. It is only meaningful for
    /// the records that carry customer interaction history; the infrastructure tables that carry delivery
    /// bookkeeping are not evidence of anything and holding them serves no governance purpose.
    /// </summary>
    protected virtual bool IsSubjectToLegalHold => false;

    /// <summary>
    /// Gets a value indicating whether this entity is held by the projection replay horizon. Only the durable
    /// event log is, because it is the only table projections can be rebuilt from.
    /// </summary>
    protected virtual bool IsSubjectToReplayHorizon => false;

    /// <summary>
    /// Gets the configured retention window, in days, for this entity.
    /// </summary>
    /// <param name="options">The configured retention options.</param>
    /// <returns>The window in days, where zero or less means this entity is never purged.</returns>
    protected abstract double GetRetentionDays(ContactCenterRetentionOptions options);

    /// <summary>
    /// Gets an additional floor, in days, that is specific to this entity and applies on top of the governance
    /// floors. The default is no additional floor.
    /// </summary>
    /// <param name="options">The configured retention options.</param>
    /// <returns>The entity-specific floor in days.</returns>
    protected virtual double GetEntityFloorDays(ContactCenterRetentionOptions options) => 0;

    /// <summary>
    /// Builds the predicate that selects records eligible for purging. It must exclude records that are still
    /// live, because age alone never makes an in-flight record safe to delete.
    /// </summary>
    /// <param name="cutoffUtc">The exclusive UTC cutoff.</param>
    /// <returns>The expired-record predicate.</returns>
    protected abstract Expression<Func<TIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc);

    /// <inheritdoc/>
    public bool TryGetCutoff(DateTime nowUtc, ContactCenterRetentionOptions options, out DateTime cutoffUtc)
    {
        ArgumentNullException.ThrowIfNull(options);

        var floorDays = GetEntityFloorDays(options);

        if (IsSubjectToLegalHold)
        {
            floorDays = Math.Max(floorDays, options.LegalHoldMinimumDays);
        }

        if (IsSubjectToReplayHorizon)
        {
            floorDays = Math.Max(floorDays, options.ProjectionReplayHorizonDays);
        }

        return RetentionCutoffCalculator.TryComputeCutoff(nowUtc, GetRetentionDays(options), floorDays, out cutoffUtc);
    }

    /// <inheritdoc/>
    public LambdaExpression GetExpiredPredicate(DateTime cutoffUtc) => BuildExpiredPredicate(cutoffUtc);

    /// <summary>
    /// Prepares an expired record for deletion, giving the entity a chance to run deletion side effects (such as
    /// enqueuing dependent cleanup for data the row points at) and to veto deletion for records that must be
    /// preserved. The default proceeds with deletion and runs no side effects.
    /// </summary>
    /// <param name="record">The expired record that is about to be deleted.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> to proceed with deleting the record; otherwise, <see langword="false"/> to skip it.</returns>
    protected virtual Task<bool> TryPrepareForDeletionAsync(TModel record, CancellationToken cancellationToken)
        => Task.FromResult(true);

    /// <inheritdoc/>
    public async Task<int> PurgeBatchAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken = default)
    {
        var take = batchSize <= 0 ? 100 : batchSize;

        var expired = await _session
            .Query<TModel, TIndex>(BuildExpiredPredicate(cutoffUtc), collection: ContactCenterStorage.CollectionName)
            .Take(take)
            .ListAsync(cancellationToken);

        var purged = 0;

        foreach (var record in expired)
        {
            try
            {
                // A record can veto its own deletion (for example a recording under legal hold) or run deletion side
                // effects (for example enqueuing media deletion) before the row is removed. A vetoed record is skipped
                // rather than deleted, and the side effects share this batch's unit of work so they commit atomically
                // with the delete once the whole batch succeeds. Both the prepare step and the delete are guarded so
                // that a failure anywhere in the batch is reported to the caller, which discards the entire batch
                // instead of letting a half-staged record leak into an unrelated entity's later flush.
                if (!await TryPrepareForDeletionAsync(record, cancellationToken))
                {
                    continue;
                }

                await _catalog.DeleteAsync(record, cancellationToken);
            }
            catch (Exception ex)
            {
                // A record failed after staging some of its side effects, and earlier records in this batch are staged
                // too. The shared session cannot selectively withdraw the staged work, so the batch is reported as
                // failed and the caller discards the whole batch rather than committing any of it; the count is carried
                // only for diagnostics. Every record this batch touched is retried cleanly on the next cycle.
                throw new ContactCenterRetentionBatchException(EntityName, purged, ex);
            }

            purged++;
        }

        return purged;
    }
}
