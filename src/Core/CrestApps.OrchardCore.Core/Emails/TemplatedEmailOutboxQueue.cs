using CrestApps.OrchardCore.WorkQueues;
using CrestApps.OrchardCore.YesSql.Core.WorkQueues;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Core.Emails;

/// <summary>
/// The leased work queue of the templated email outbox, on its own index: new emails join the caller's unit of work,
/// claims and settlements commit in sessions of their own.
/// </summary>
internal sealed class TemplatedEmailOutboxQueue : YesSqlLeasedWorkQueue<TemplatedEmailOutboxItem, TemplatedEmailOutboxIndex>
{
    /// <summary>
    /// How the queue keeps its emails: six attempts in all (the first and five retries), and sent emails kept as
    /// completed until the retention policy removes them.
    /// </summary>
    internal static readonly LeasedWorkQueueOptions QueueOptions = new()
    {
        CollectionName = TemplatedEmailOutboxStorage.CollectionName,
        MaxAttempts = TemplatedEmailOutbox.MaxAttempts,
        RemoveCompletedItems = false,
    };

    public TemplatedEmailOutboxQueue(IStore store, ISession session, IClock clock)
        : base(store, session, clock, QueueOptions)
    {
    }

    /// <inheritdoc/>
    protected override IQuery<TemplatedEmailOutboxItem> QueryDue(ISession session, DateTime nowUtc)
        => session.Query<TemplatedEmailOutboxItem, TemplatedEmailOutboxIndex>(
            index => (index.Status == LeasedWorkItemStatus.Pending || index.Status == LeasedWorkItemStatus.Claimed) &&
                index.NextAttemptUtc <= nowUtc,
            collection: TemplatedEmailOutboxStorage.CollectionName)
            .OrderBy(index => index.NextAttemptUtc)
            .ThenBy(index => index.Id);
}
