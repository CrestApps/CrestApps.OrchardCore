using CrestApps.OrchardCore.Webhooks.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Webhooks.Core.Indexes;

/// <summary>
/// Maps webhook subscriptions to <see cref="WebhookSubscriptionIndex"/>.
/// </summary>
public sealed class WebhookSubscriptionIndexProvider : IndexProvider<WebhookSubscription>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookSubscriptionIndexProvider"/> class.
    /// </summary>
    public WebhookSubscriptionIndexProvider()
    {
        CollectionName = WebhookStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<WebhookSubscription> context)
        => context.For<WebhookSubscriptionIndex>()
            .Map(subscription => new WebhookSubscriptionIndex
            {
                ItemId = subscription.ItemId,
                IsEnabled = subscription.IsEnabled,
                CreatedUtc = subscription.CreatedUtc,
            });
}
