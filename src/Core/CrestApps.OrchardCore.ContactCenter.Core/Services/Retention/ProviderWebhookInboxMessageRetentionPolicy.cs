using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Purges provider webhook deliveries that have been processed or dead-lettered, measured from settlement.
/// A delivery that is still pending is retained regardless of age so an outage backlog is not discarded.
/// A completed row is the idempotency tombstone that makes a provider redelivery a duplicate, so it is held for
/// at least the redelivery envelope: deleting it early makes a redelivered webhook look new and runs its side
/// effect a second time.
/// </summary>
public sealed class ProviderWebhookInboxMessageRetentionPolicy : ContactCenterRetentionPolicyBase<ProviderWebhookInboxMessage, ProviderWebhookInboxMessageIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderWebhookInboxMessageRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="inboxStore">The provider webhook inbox store.</param>
    public ProviderWebhookInboxMessageRetentionPolicy(
        ISession session,
        IProviderWebhookInboxStore inboxStore)
        : base(session, inboxStore)
    {
    }

    /// <inheritdoc/>
    public override string EntityName => "ProviderWebhookInboxMessage";

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.WebhookInboxMessageRetentionDays;

    /// <inheritdoc/>
    protected override double GetEntityFloorDays(ContactCenterRetentionOptions options)
        => Math.Max(options.ProcessedEventDeliveryEnvelopeDays, ProviderWebhookInbox.TombstoneRetentionDays);

    /// <inheritdoc/>
    protected override Expression<Func<ProviderWebhookInboxMessageIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.ProcessedUtc != null
            && index.ProcessedUtc < cutoffUtc
            && (index.Status == ProviderWebhookInboxStatus.Completed || index.Status == ProviderWebhookInboxStatus.DeadLettered);
}
