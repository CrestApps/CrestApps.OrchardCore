using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Purges outbox messages that have been delivered or dead-lettered, measured from creation. The retry
/// time cannot serve as the age because a settled message keeps whatever retry time it last held.
/// </summary>
public sealed class ContactCenterOutboxMessageRetentionPolicy : ContactCenterRetentionPolicyBase<ContactCenterOutboxMessage, ContactCenterOutboxMessageIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterOutboxMessageRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="outboxStore">The outbox store.</param>
    public ContactCenterOutboxMessageRetentionPolicy(
        ISession session,
        IContactCenterOutboxStore outboxStore)
        : base(session, outboxStore)
    {
    }

    /// <inheritdoc/>
    public override string EntityName => "ContactCenterOutboxMessage";

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.OutboxMessageRetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<ContactCenterOutboxMessageIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.CreatedUtc < cutoffUtc
            && (index.Status == OutboxMessageStatus.Completed || index.Status == OutboxMessageStatus.DeadLettered);
}
