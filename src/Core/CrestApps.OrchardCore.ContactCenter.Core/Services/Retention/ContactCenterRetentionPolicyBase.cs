using System.Linq.Expressions;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Retention;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Provides the shared batching, cutoff, and deletion behavior for a document-backed retention policy so each
/// entity only has to declare its window, its floors, and what makes one of its records expired.
/// </summary>
/// <typeparam name="TModel">The catalog item type being purged.</typeparam>
/// <typeparam name="TIndex">The index type whose table carries the age column.</typeparam>
public abstract class ContactCenterRetentionPolicyBase<TModel, TIndex> : DocumentRetentionPolicyBase<TModel, TIndex>, IContactCenterRetentionPolicy
    where TModel : CatalogItem
    where TIndex : CatalogItemIndex
{
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
        : base(session, ContactCenterStorage.CollectionName)
    {
        _catalog = catalog;
    }

    /// <inheritdoc/>
    public abstract string EntityName { get; }

    /// <inheritdoc/>
    public override string Name => EntityName;

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
    /// Contact Center windows come from <see cref="ContactCenterRetentionOptions"/>, which only the Contact Center
    /// retention service passes, so without them a policy keeps its records.
    /// </summary>
    /// <returns>Zero, which keeps the records.</returns>
    protected sealed override double GetRetentionDays() => 0;

    /// <inheritdoc/>
    protected override async ValueTask DeleteAsync(TModel record, CancellationToken cancellationToken)
        => await _catalog.DeleteAsync(record, cancellationToken);

    /// <inheritdoc/>
    protected override Exception OnBatchFailed(int stagedCount, Exception exception)
        => new ContactCenterRetentionBatchException(EntityName, stagedCount, exception);
}