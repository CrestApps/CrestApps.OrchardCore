using System.Linq.Expressions;
using CrestApps.OrchardCore.Outreach.Core.Indexes;
using CrestApps.OrchardCore.Outreach.Core.Models;
using CrestApps.OrchardCore.WorkQueues;
using CrestApps.OrchardCore.YesSql.Core.Retention;
using YesSql;

namespace CrestApps.OrchardCore.Outreach.Core.Services;

/// <summary>
/// Removes outreach batches 30 days after they completed or were given up on. The run keeps the totals.
/// </summary>
public sealed class OutreachBatchRetentionPolicy : DocumentRetentionPolicyBase<OutreachBatch, OutreachBatchIndex>
{
    /// <summary>
    /// The days a settled batch is kept.
    /// </summary>
    public const double RetentionDays = 30;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutreachBatchRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant session.</param>
    public OutreachBatchRetentionPolicy(ISession session)
        : base(session, OutreachConstants.CollectionName)
    {
    }

    /// <inheritdoc/>
    public override string Name
        => "OutreachBatches";

    /// <inheritdoc/>
    protected override double GetRetentionDays()
        => RetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<OutreachBatchIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => (index.Status == LeasedWorkItemStatus.Completed || index.Status == LeasedWorkItemStatus.DeadLettered) &&
            index.NextAttemptUtc < cutoffUtc;
}
