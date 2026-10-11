using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;

namespace CrestApps.OrchardCore.Tests.Subscriptions.Fakes;

/// <summary>
/// An in-memory <see cref="ITenantProvisioningJobStore"/> so provisioning behavior can be tested without a
/// database.
/// </summary>
internal sealed class InMemoryTenantProvisioningJobStore : ITenantProvisioningJobStore
{
    private readonly Dictionary<string, TenantProvisioningJob> _jobs = new(StringComparer.Ordinal);

    public InMemoryTenantProvisioningJobStore(params TenantProvisioningJob[] seed)
    {
        foreach (var job in seed)
        {
            if (string.IsNullOrEmpty(job.ItemId))
            {
                job.ItemId = UniqueId.GenerateId();
            }

            _jobs[job.ItemId] = job;
        }
    }

    public IReadOnlyCollection<TenantProvisioningJob> Jobs
        => _jobs.Values;

    public ValueTask CreateAsync(TenantProvisioningJob entry, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(entry.ItemId))
        {
            entry.ItemId = UniqueId.GenerateId();
        }

        _jobs[entry.ItemId] = entry;

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(TenantProvisioningJob entry, CancellationToken cancellationToken = default)
    {
        _jobs[entry.ItemId] = entry;

        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> DeleteAsync(TenantProvisioningJob entry, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_jobs.Remove(entry.ItemId));

    public ValueTask<TenantProvisioningJob> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_jobs.GetValueOrDefault(id ?? string.Empty));

    public ValueTask<IReadOnlyCollection<TenantProvisioningJob>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<TenantProvisioningJob>>([.. _jobs.Values.Where(job => ids.Contains(job.ItemId, StringComparer.Ordinal))]);

    public ValueTask<IReadOnlyCollection<TenantProvisioningJob>> GetAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<TenantProvisioningJob>>([.. _jobs.Values]);

    public ValueTask<PageResult<TenantProvisioningJob>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
        where TQuery : QueryContext
    {
        var entries = _jobs.Values.ToArray();

        return ValueTask.FromResult(new PageResult<TenantProvisioningJob>
        {
            Count = entries.Length,
            Entries = entries,
        });
    }

    public Task<TenantProvisioningJob> GetByCheckoutSessionAsync(string checkoutSessionId, CancellationToken cancellationToken = default)
        => Task.FromResult(_jobs.Values.FirstOrDefault(job => job.CheckoutSessionId == checkoutSessionId));

    public Task<TenantProvisioningJob> GetByTenantNameAsync(string tenantName, CancellationToken cancellationToken = default)
        => Task.FromResult(_jobs.Values.FirstOrDefault(job => job.TenantName == tenantName));

    public Task<IReadOnlyList<TenantProvisioningJob>> GetDueAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TenantProvisioningJob>>(
            [.. _jobs.Values
                .Where(job =>
                    job.Status is TenantProvisioningStatus.Pending or TenantProvisioningStatus.Failed or TenantProvisioningStatus.Running &&
                    (job.NextAttemptUtc is null || job.NextAttemptUtc <= asOfUtc))
                .OrderBy(job => job.CreatedUtc)]);

    public Task<IReadOnlyList<TenantProvisioningJob>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TenantProvisioningJob>>([.. _jobs.Values.Where(job => job.OwnerId == ownerId)]);
}
