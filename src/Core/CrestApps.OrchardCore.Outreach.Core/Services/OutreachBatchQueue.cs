using CrestApps.OrchardCore.Outreach.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.WorkQueues;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Outreach.Core.Services;

/// <summary>
/// Creates the leased work queue of outreach batches. Every batch runs in an isolated scope, so the queue commits in that
/// scope's session, together with what the batch changed.
/// </summary>
internal static class OutreachBatchQueue
{
    /// <summary>
    /// The failed attempts a batch gets before its run fails.
    /// </summary>
    public const int MaxAttempts = 5;

    // Completed batches stay, with what each sent, until their run is purged.
    private static readonly LeasedWorkQueueOptions _options = new()
    {
        CollectionName = OutreachConstants.CollectionName,
        MaxAttempts = MaxAttempts,
    };

    public static CatalogLeasedWorkQueue<OutreachBatch> Create(IOutreachBatchStore store, ISession session, IClock clock)
        => new(store, session, clock, _options, store.GetDueAsync);
}
