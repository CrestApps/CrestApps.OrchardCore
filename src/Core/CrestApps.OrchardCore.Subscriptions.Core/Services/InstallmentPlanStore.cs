using CrestApps.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// The YesSql-backed <see cref="IInstallmentPlanStore"/>. Plans are concurrency-checked: the schedule sweep, the
/// payment handler and an administrator can all change the same plan, and none may silently undo another.
/// </summary>
public sealed class InstallmentPlanStore : DocumentCatalog<InstallmentPlan, InstallmentPlanIndex>, IInstallmentPlanStore
{
    private readonly IClock _clock;

    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallmentPlanStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="clock">The clock used to stamp the plan.</param>
    public InstallmentPlanStore(ISession session, IClock clock)
        : base(session)
    {
        CollectionName = SubscriptionConstants.InstallmentPlanCollectionName;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<PageResult<InstallmentPlan>> PageAsync(int page, int pageSize, InstallmentPlanQuery query, CancellationToken cancellationToken = default)
    {
        query ??= new InstallmentPlanQuery();

        var records = Session.Query<InstallmentPlan, InstallmentPlanIndex>(collection: CollectionName);

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;

            records = records.Where(x => x.Status == status);
        }

        if (!string.IsNullOrEmpty(query.OwnerId))
        {
            var ownerId = query.OwnerId;

            records = records.Where(x => x.OwnerId == ownerId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            records = records.Where(x => x.Title.Contains(search) || x.CustomerName.Contains(search) || x.CustomerEmail.Contains(search));
        }

        var count = await records.CountAsync(cancellationToken);
        var skip = (Math.Max(1, page) - 1) * pageSize;

        return new PageResult<InstallmentPlan>
        {
            Count = count,
            Entries = (await records.OrderByDescending(x => x.CreatedUtc).Skip(skip).Take(pageSize).ListAsync(cancellationToken)).ToArray(),
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InstallmentPlan>> GetOpenAsync(CancellationToken cancellationToken = default)
    {
        var records = await Session.Query<InstallmentPlan, InstallmentPlanIndex>(
            x => x.Status == InstallmentPlanStatus.Draft || x.Status == InstallmentPlanStatus.Active || x.Status == InstallmentPlanStatus.PastDue,
            collection: CollectionName)
            .OrderBy(x => x.NextDueUtc)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InstallmentPlan>> GetByReferenceAsync(string referenceType, string referenceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(referenceType);
        ArgumentException.ThrowIfNullOrEmpty(referenceId);

        var type = InstallmentPlanIndexProvider.Truncate(referenceType, InstallmentPlanIndexProvider.ReferenceMaxLength);
        var id = InstallmentPlanIndexProvider.Truncate(referenceId, InstallmentPlanIndexProvider.ReferenceMaxLength);

        var records = await Session.Query<InstallmentPlan, InstallmentPlanIndex>(
            x => x.ReferenceType == type && x.ReferenceId == id,
            collection: CollectionName)
            .OrderByDescending(x => x.CreatedUtc)
            .ListAsync(cancellationToken);

        // The index holds a truncated value; the plan holds the whole one, so a longer reference is matched exactly here.
        return records
            .Where(plan =>
                string.Equals(plan.ReferenceType, referenceType, StringComparison.Ordinal) &&
                string.Equals(plan.ReferenceId, referenceId, StringComparison.Ordinal))
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InstallmentPlan>> GetBySourceTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(transactionId);

        var records = await Session.Query<InstallmentPlan, InstallmentPlanIndex>(
            x => x.SourceTransactionId == transactionId,
            collection: CollectionName)
            .OrderByDescending(x => x.CreatedUtc)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }

    /// <inheritdoc/>
    protected override ValueTask SavingAsync(InstallmentPlan record)
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
