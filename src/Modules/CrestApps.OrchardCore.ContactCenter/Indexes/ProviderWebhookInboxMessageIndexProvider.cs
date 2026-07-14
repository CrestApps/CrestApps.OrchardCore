using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps provider webhook inbox documents to their idempotency and retry index.
/// </summary>
public sealed class ProviderWebhookInboxMessageIndexProvider : IndexProvider<ProviderWebhookInboxMessage>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderWebhookInboxMessageIndexProvider"/> class.
    /// </summary>
    public ProviderWebhookInboxMessageIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ProviderWebhookInboxMessage> context)
    {
        context
            .For<ProviderWebhookInboxMessageIndex>()
            .Map(message => new ProviderWebhookInboxMessageIndex
            {
                ItemId = message.ItemId,
                ProviderName = message.ProviderName,
                DeliveryId = message.DeliveryId,
                Status = message.Status,
                NextAttemptUtc = message.NextAttemptUtc,
            });
    }
}
