using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps provider webhook inbox documents to their idempotency and retry index.
/// </summary>
public sealed class ProviderWebhookInboxMessageIndexProvider : IndexProvider<ProviderWebhookInboxMessage>
{
    private readonly IProviderIdentityResolver _providerIdentityResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderWebhookInboxMessageIndexProvider"/> class.
    /// </summary>
    /// <param name="providerIdentityResolver">The resolver used to canonicalize provider aliases so legacy documents cannot recreate alias delivery index values on reindex.</param>
    public ProviderWebhookInboxMessageIndexProvider(IProviderIdentityResolver providerIdentityResolver)
    {
        _providerIdentityResolver = providerIdentityResolver;
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
                ProviderName = _providerIdentityResolver.Canonicalize(message.ProviderName),
                DeliveryId = message.DeliveryId,
                Status = message.Status,
                NextAttemptUtc = message.NextAttemptUtc,
            });
    }
}
