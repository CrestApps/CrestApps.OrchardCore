using System.Linq.Expressions;
using CrestApps.OrchardCore.WorkQueues;
using CrestApps.OrchardCore.YesSql.Core.Retention;
using YesSql;

namespace CrestApps.OrchardCore.Core.Emails;

/// <summary>
/// Removes the outbox's emails that were sent or given up more than <see cref="RetentionDays"/> days ago. An email still
/// waiting for an attempt is never removed.
/// </summary>
public sealed class TemplatedEmailOutboxRetentionPolicy : DocumentRetentionPolicyBase<TemplatedEmailOutboxItem, TemplatedEmailOutboxIndex>
{
    /// <summary>
    /// How long a sent or given-up email is kept, in days.
    /// </summary>
    public const double RetentionDays = 30;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplatedEmailOutboxRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant session.</param>
    public TemplatedEmailOutboxRetentionPolicy(ISession session)
        : base(session, TemplatedEmailOutboxStorage.CollectionName)
    {
    }

    /// <inheritdoc/>
    public override string Name => "TemplatedEmailOutbox";

    /// <inheritdoc/>
    protected override double GetRetentionDays() => RetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<TemplatedEmailOutboxIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => (index.Status == LeasedWorkItemStatus.Completed || index.Status == LeasedWorkItemStatus.DeadLettered)
            && index.FinishedUtc != null
            && index.FinishedUtc < cutoffUtc;
}
