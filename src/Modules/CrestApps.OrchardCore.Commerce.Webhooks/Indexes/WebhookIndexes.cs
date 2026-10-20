using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Commerce.Webhooks.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Commerce.Webhooks.Indexes;

public sealed class WebhookSubscriptionIndex : CatalogItemIndex
{
    public bool IsEnabled { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class WebhookSubscriptionIndexProvider : IndexProvider<WebhookSubscription>
{
    public WebhookSubscriptionIndexProvider()
    {
        CollectionName = WebhookConstants.CollectionName;
    }

    public override void Describe(DescribeContext<WebhookSubscription> context)
        => context.For<WebhookSubscriptionIndex>()
            .Map(subscription => new WebhookSubscriptionIndex
            {
                ItemId = subscription.ItemId,
                IsEnabled = subscription.IsEnabled,
                CreatedUtc = subscription.CreatedUtc,
            });
}

public sealed class WebhookDeliveryIndex : CatalogItemIndex
{
    public string SubscriptionId { get; set; }

    public string EventName { get; set; }

    public int Status { get; set; }

    public DateTime? NextAttemptUtc { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class WebhookDeliveryIndexProvider : IndexProvider<WebhookDelivery>
{
    public WebhookDeliveryIndexProvider()
    {
        CollectionName = WebhookConstants.CollectionName;
    }

    public override void Describe(DescribeContext<WebhookDelivery> context)
        => context.For<WebhookDeliveryIndex>()
            .Map(delivery => new WebhookDeliveryIndex
            {
                ItemId = delivery.ItemId,
                SubscriptionId = delivery.SubscriptionId,
                EventName = delivery.EventName,
                Status = (int)delivery.Status,
                NextAttemptUtc = delivery.Status == WebhookDeliveryStatus.Pending ? delivery.NextAttemptUtc : null,
                CreatedUtc = delivery.CreatedUtc,
            });
}
