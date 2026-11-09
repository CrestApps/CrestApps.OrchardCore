using CrestApps.Core.Models;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.YesSql.Core.WorkQueues;

/// <summary>
/// The leased work queue of a work item type indexed by the shared <see cref="LeasedWorkItemIndex"/>. Register it with
/// <c>AddLeasedWorkQueue</c>, and create the index table of the collection with <see cref="LeasedWorkItemIndexSchema"/>.
/// </summary>
/// <typeparam name="T">The work item type.</typeparam>
public sealed class LeasedWorkQueue<T> : YesSqlLeasedWorkQueue<T, LeasedWorkItemIndex>
    where T : CatalogItem, ILeasedWorkItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LeasedWorkQueue{T}"/> class.
    /// </summary>
    /// <param name="store">The tenant store.</param>
    /// <param name="session">The caller's session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="options">The options of this item type's queue.</param>
    public LeasedWorkQueue(IStore store, ISession session, IClock clock, IOptions<LeasedWorkQueueOptions<T>> options)
        : base(store, session, clock, options.Value)
    {
    }

    /// <inheritdoc/>
    protected override IQuery<T> QueryDue(ISession session, DateTime nowUtc)
        => session.Query<T, LeasedWorkItemIndex>(
            index => (index.Status == LeasedWorkItemStatus.Pending || index.Status == LeasedWorkItemStatus.Claimed) &&
                index.NextAttemptUtc <= nowUtc,
            collection: Options.CollectionName)
            .OrderBy(index => index.NextAttemptUtc)
            .ThenBy(index => index.Id);
}
