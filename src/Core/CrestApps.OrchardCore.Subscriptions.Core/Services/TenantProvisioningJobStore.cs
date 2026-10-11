using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// The default YesSql-backed <see cref="ITenantProvisioningJobStore"/>.
/// </summary>
public sealed class TenantProvisioningJobStore : DocumentCatalog<TenantProvisioningJob, TenantProvisioningJobIndex>, ITenantProvisioningJobStore
{
    private readonly IClock _clock;

    /// <summary>
    /// Enables document-version concurrency checks so two nodes cannot both claim the same job and create
    /// the tenant twice.
    /// </summary>
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantProvisioningJobStore"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session.</param>
    /// <param name="clock">The clock used for timestamps.</param>
    public TenantProvisioningJobStore(ISession session, IClock clock)
        : base(session)
    {
        CollectionName = SubscriptionConstants.TenantProvisioningCollectionName;
        _clock = clock;
    }

    /// <inheritdoc/>
    public Task<TenantProvisioningJob> GetByCheckoutSessionAsync(string checkoutSessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(checkoutSessionId);

        return Session
            .Query<TenantProvisioningJob, TenantProvisioningJobIndex>(x => x.CheckoutSessionId == checkoutSessionId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TenantProvisioningJob> GetByTenantNameAsync(string tenantName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantName);

        return Session
            .Query<TenantProvisioningJob, TenantProvisioningJobIndex>(x => x.TenantName == tenantName, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenantProvisioningJob>> GetDueAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        // A job left Running by a process that died is picked up again once its back-off passes. Without
        // that, a restart mid-setup would strand a paid-for site forever.
        var records = await Session
            .Query<TenantProvisioningJob, TenantProvisioningJobIndex>(
                x => (x.Status == TenantProvisioningStatus.Pending ||
                        x.Status == TenantProvisioningStatus.Failed ||
                        x.Status == TenantProvisioningStatus.Running) &&
                    (x.NextAttemptUtc == null || x.NextAttemptUtc <= asOfUtc),
                collection: CollectionName)
            .OrderBy(x => x.CreatedUtc)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenantProvisioningJob>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerId);

        var records = await Session
            .Query<TenantProvisioningJob, TenantProvisioningJobIndex>(x => x.OwnerId == ownerId, collection: CollectionName)
            .OrderByDescending(x => x.CreatedUtc)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }

    /// <inheritdoc/>
    protected override ValueTask SavingAsync(TenantProvisioningJob record)
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
