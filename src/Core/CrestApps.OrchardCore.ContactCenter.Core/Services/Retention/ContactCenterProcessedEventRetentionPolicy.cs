using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Purges event deduplication markers. A marker may only be purged once no redelivery of the same event can
/// still arrive, so the delivery envelope acts as a floor beneath the configured window. Purging a marker
/// early makes an already-processed event look new and lets its side effect run a second time.
/// </summary>
public sealed class ContactCenterProcessedEventRetentionPolicy : ContactCenterRetentionPolicyBase<ContactCenterProcessedEvent, ContactCenterProcessedEventIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterProcessedEventRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="processedEventStore">The processed event store.</param>
    public ContactCenterProcessedEventRetentionPolicy(
        ISession session,
        IContactCenterProcessedEventStore processedEventStore)
        : base(session, processedEventStore)
    {
    }

    /// <inheritdoc/>
    public override string EntityName => "ContactCenterProcessedEvent";

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.ProcessedEventRetentionDays;

    /// <inheritdoc/>
    protected override double GetEntityFloorDays(ContactCenterRetentionOptions options)
        => options.ProcessedEventDeliveryEnvelopeDays;

    /// <inheritdoc/>
    protected override Expression<Func<ContactCenterProcessedEventIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.ProcessedUtc < cutoffUtc;
}
