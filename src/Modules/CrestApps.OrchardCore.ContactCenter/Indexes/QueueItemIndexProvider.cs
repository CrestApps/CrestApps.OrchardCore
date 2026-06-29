using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="QueueItem"/> documents to the <see cref="QueueItemIndex"/>.
/// </summary>
public sealed class QueueItemIndexProvider : IndexProvider<QueueItem>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueueItemIndexProvider"/> class.
    /// </summary>
    public QueueItemIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<QueueItem> context)
    {
        context
            .For<QueueItemIndex>()
            .Map(item => new QueueItemIndex
            {
                ItemId = item.ItemId,
                QueueId = item.QueueId,
                ActivityItemId = item.ActivityItemId,
                Status = item.Status,
                Priority = item.Priority,
                AgentId = item.AgentId,
                EnqueuedUtc = item.EnqueuedUtc,
            });
    }
}
