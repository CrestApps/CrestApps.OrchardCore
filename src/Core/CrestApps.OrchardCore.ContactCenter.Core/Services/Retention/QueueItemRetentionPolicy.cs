using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Purges queue items that have left the queue, measured from the time they were dequeued. Purging by
/// arrival time instead would delete an item the moment it was handled if it had waited longer than the
/// window, destroying exactly the records that describe the worst waits.
/// </summary>
public sealed class QueueItemRetentionPolicy : ContactCenterRetentionPolicyBase<QueueItem, QueueItemIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueueItemRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="queueItemStore">The queue item store.</param>
    public QueueItemRetentionPolicy(
        ISession session,
        IQueueItemStore queueItemStore)
        : base(session, queueItemStore)
    {
    }

    /// <inheritdoc/>
    public override string EntityName => "QueueItem";

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.QueueItemRetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<QueueItemIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.DequeuedUtc != null
            && index.DequeuedUtc < cutoffUtc
            && (index.Status == QueueItemStatus.Completed || index.Status == QueueItemStatus.Removed);
}
