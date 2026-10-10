using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;

namespace CrestApps.OrchardCore.Tests.Subscriptions.Fakes;

/// <summary>
/// An in-memory <see cref="IInstallmentPlanStore"/> so plan behavior can be tested without a database.
/// </summary>
internal sealed class InMemoryInstallmentPlanStore : IInstallmentPlanStore
{
    private readonly Dictionary<string, InstallmentPlan> _plans = new(StringComparer.Ordinal);

    public IReadOnlyCollection<InstallmentPlan> Plans
        => _plans.Values;

    public int UpdateCount { get; private set; }

    public ValueTask CreateAsync(InstallmentPlan entry, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(entry.ItemId))
        {
            entry.ItemId = UniqueId.GenerateId();
        }

        _plans[entry.ItemId] = entry;

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(InstallmentPlan entry, CancellationToken cancellationToken = default)
    {
        UpdateCount++;
        _plans[entry.ItemId] = entry;

        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> DeleteAsync(InstallmentPlan entry, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_plans.Remove(entry.ItemId));

    public ValueTask<InstallmentPlan> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_plans.GetValueOrDefault(id ?? string.Empty));

    public ValueTask<IReadOnlyCollection<InstallmentPlan>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<InstallmentPlan>>([.. _plans.Values.Where(plan => ids.Contains(plan.ItemId, StringComparer.Ordinal))]);

    public ValueTask<IReadOnlyCollection<InstallmentPlan>> GetAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<InstallmentPlan>>([.. _plans.Values]);

    public ValueTask<PageResult<InstallmentPlan>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
        where TQuery : QueryContext
        => ValueTask.FromResult(new PageResult<InstallmentPlan> { Count = _plans.Count, Entries = [.. _plans.Values] });

    public Task<PageResult<InstallmentPlan>> PageAsync(int page, int pageSize, InstallmentPlanQuery query, CancellationToken cancellationToken = default)
    {
        var matches = _plans.Values
            .Where(plan => query?.Status is null || plan.Status == query.Status)
            .Where(plan => string.IsNullOrEmpty(query?.OwnerId) || plan.OwnerId == query.OwnerId)
            .OrderByDescending(plan => plan.CreatedUtc)
            .ToArray();

        return Task.FromResult(new PageResult<InstallmentPlan>
        {
            Count = matches.Length,
            Entries = [.. matches.Skip(Math.Max(0, page - 1) * pageSize).Take(pageSize)],
        });
    }

    public Task<IReadOnlyList<InstallmentPlan>> GetOpenAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<InstallmentPlan>>(
            [.. _plans.Values.Where(plan => plan.Status is InstallmentPlanStatus.Draft or InstallmentPlanStatus.Active or InstallmentPlanStatus.PastDue)]);
}
