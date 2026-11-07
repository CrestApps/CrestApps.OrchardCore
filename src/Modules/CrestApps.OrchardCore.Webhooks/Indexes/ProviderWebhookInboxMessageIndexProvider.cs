using CrestApps.OrchardCore.Webhooks.Core;
using CrestApps.OrchardCore.Webhooks.Core.Indexes;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Webhooks.Indexes;

/// <summary>
/// Maps provider webhook inbox documents to their idempotency and retry index.
/// </summary>
public sealed class ProviderWebhookInboxMessageIndexProvider : IndexProvider<ProviderWebhookInboxMessage>
{
    private readonly IWebhookProviderNameNormalizer _providerNameNormalizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderWebhookInboxMessageIndexProvider"/> class.
    /// </summary>
    /// <param name="providerNameNormalizer">The normalizer used to canonicalize provider aliases so legacy documents cannot recreate alias delivery index values on reindex.</param>
    public ProviderWebhookInboxMessageIndexProvider(IWebhookProviderNameNormalizer providerNameNormalizer)
    {
        _providerNameNormalizer = providerNameNormalizer;
        CollectionName = ProviderWebhookInboxStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ProviderWebhookInboxMessage> context)
    {
        context
            .For<ProviderWebhookInboxMessageIndex>()
            .Map(message => new ProviderWebhookInboxMessageIndex
            {
                ItemId = message.ItemId,
                ProviderName = _providerNameNormalizer.Normalize(message.ProviderName),
                DeliveryId = message.DeliveryId,
                Status = message.Status,
                NextAttemptUtc = message.NextAttemptUtc,
                ProcessedUtc = message.ProcessedUtc,
            });
    }
}
