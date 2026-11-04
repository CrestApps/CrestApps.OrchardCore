using CrestApps.OrchardCore.Webhooks.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Webhooks.Core.Indexes;

/// <summary>
/// Maps webhook deliveries to <see cref="WebhookDeliveryIndex"/>.
/// </summary>
public sealed class WebhookDeliveryIndexProvider : IndexProvider<WebhookDelivery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookDeliveryIndexProvider"/> class.
    /// </summary>
    public WebhookDeliveryIndexProvider()
    {
        CollectionName = WebhookStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<WebhookDelivery> context)
        => context.For<WebhookDeliveryIndex>()
            .Map(delivery => new WebhookDeliveryIndex
            {
                ItemId = delivery.ItemId,
                SubscriptionId = delivery.SubscriptionId,
                EventName = delivery.EventName,
                Status = delivery.Status,
                NextAttemptUtc = delivery.Status == WebhookDeliveryStatus.Pending ? delivery.NextAttemptUtc : null,
                CreatedUtc = delivery.CreatedUtc,
            });
}
