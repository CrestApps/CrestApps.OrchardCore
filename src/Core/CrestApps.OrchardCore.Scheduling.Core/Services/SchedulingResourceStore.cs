using CrestApps.OrchardCore.Scheduling.Core.Indexes;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Scheduling.Core.Services;

/// <summary>
/// Stores schedulable resources.
/// </summary>
public sealed class SchedulingResourceStore : DocumentCatalog<SchedulingResource, SchedulingResourceIndex>
{
    // Bookings bump a resource's version under a concurrency check, so two can't take the same time.
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchedulingResourceStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SchedulingResourceStore(ISession session)
        : base(session)
    {
        CollectionName = SchedulingConstants.CollectionName;
    }

    /// <summary>
    /// Lists every resource, by name.
    /// </summary>
    /// <returns>The resources.</returns>
    public Task<IReadOnlyList<SchedulingResource>> ListAsync()
    {
        return ListByNameAsync(index => index.Name);
    }
}
