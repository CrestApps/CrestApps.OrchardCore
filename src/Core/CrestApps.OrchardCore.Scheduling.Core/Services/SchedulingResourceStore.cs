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

    /// <summary>
    /// Returns a page of the records ordered by name, and how many there are in all, paged and counted in SQL.
    /// </summary>
    /// <param name="skip">How many records to skip.</param>
    /// <param name="take">How many records to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many records there are, and the page of them.</returns>
    public Task<(int Count, IReadOnlyList<SchedulingResource> Items)> PageAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        return PageByNameAsync(index => index.Name, search: null, skip, take, cancellationToken);
    }
}
