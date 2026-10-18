using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Indexes the dialer profile of the queue items still in a queue that were saved before the queue item index carried
/// it, so the dialer queue inventory and the campaign summary read every item's stamped profile from the index.
/// </summary>
/// <remarks>
/// <para>
/// A queue item is indexed again whenever it is saved, so only the items nothing has touched since the upgrade need this,
/// and only those still waiting, reserved or assigned: a settled item is never read by those reports. It runs here
/// rather than in the migration that adds the column, because reading and re-saving every item of a large dialer load
/// during activation would hold up the tenant.
/// </para>
/// <para>
/// The pass is bounded per run and resumes from where it stopped. Once a pass finds nothing left it stops for the life
/// of the node; a node that starts later reads only the items that legitimately carry no profile (inbound work), which is
/// one cheap query and no writes.
/// </para>
/// </remarks>
[BackgroundTask(
    Title = "Contact Center Queue Item Dialer Profile Backfill",
    Schedule = "* * * * *",
    Description = "Indexes the dialer profile of queue items saved before the queue item index carried it.",
    LockTimeout = 5_000,
    LockExpiration = 300_000)]
public sealed class QueueItemDialerProfileIndexBackfillBackgroundTask : IBackgroundTask
{
    internal const int PageSize = 200;

    internal const int PagesPerRun = 25;

    private long _cursor;
    private bool _completed;

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        if (_completed)
        {
            return;
        }

        var workManager = serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>();
        using var workLease = workManager.TryEnter(ContactCenterConstants.Feature.Queues);

        if (workLease is null)
        {
            return;
        }

        var store = serviceProvider.GetRequiredService<IStore>();
        var logger = serviceProvider.GetRequiredService<ILogger<QueueItemDialerProfileIndexBackfillBackgroundTask>>();
        var reindexed = 0;

        for (var page = 0; page < PagesPerRun; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var session = store.CreateSession();

            var cursor = _cursor;
            var rows = (await session.QueryIndex<QueueItemIndex>(index =>
                    index.DialerProfileId == null &&
                    (index.Status == QueueItemStatus.Waiting ||
                        index.Status == QueueItemStatus.Reserved ||
                        index.Status == QueueItemStatus.Assigned) &&
                    index.DocumentId > cursor,
                    collection: ContactCenterStorage.CollectionName)
                .OrderBy(index => index.DocumentId)
                .Take(PageSize)
                .ListAsync(cancellationToken))
                .ToList();

            if (rows.Count == 0)
            {
                _completed = true;

                break;
            }

            var itemIds = rows.Select(row => row.ItemId).ToArray();
            var items = await session.Query<QueueItem, QueueItemIndex>(
                    index => index.ItemId.IsIn(itemIds),
                    collection: ContactCenterStorage.CollectionName)
                .ListAsync(cancellationToken);

            var saved = 0;

            foreach (var item in items)
            {
                // Saving the item unchanged writes its index row again, now with the dialer profile it records. The
                // concurrency check means a change made by routing meanwhile is never overwritten; that change re-indexed
                // the item anyway.
                if (!string.IsNullOrEmpty(item.DialerProfileId))
                {
                    await session.SaveAsync(item, checkConcurrency: true, collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
                    saved++;
                }
            }

            try
            {
                await session.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyException)
            {
                // Another node moved one of these items first and indexed it as it did; the page is read again next run.
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("A queue item changed while its dialer profile was being indexed; the page is indexed again on the next run.");
                }

                break;
            }

            reindexed += saved;
            _cursor = rows.Max(row => row.DocumentId);
        }

        if (reindexed > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Indexed the dialer profile of {QueueItemCount} queue items saved before the queue item index carried it.", reindexed);
        }
    }
}
