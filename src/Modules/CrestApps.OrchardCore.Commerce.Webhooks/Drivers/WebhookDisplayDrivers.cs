using CrestApps.OrchardCore.Commerce.Webhooks.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Commerce.Webhooks.Drivers;

/// <summary>
/// Renders a webhook as a row of the webhooks admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class WebhookSubscriptionDisplayDriver : DisplayDriver<WebhookSubscription>
{
    public override IDisplayResult Display(WebhookSubscription subscription, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = subscription.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("WebhookSubscription_Fields_SummaryAdmin", subscription)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("WebhookSubscription_Description_SummaryAdmin", subscription)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("WebhookSubscription_Status_SummaryAdmin", subscription)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("WebhookSubscription_Events_SummaryAdmin", subscription)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Events:1"),
            View("WebhookSubscription_Buttons_SummaryAdmin", subscription).Location("Actions:5"));
    }
}

/// <summary>
/// Renders a delivery as a row of a webhook's deliveries log, in both of its layouts (List and Grid).
/// </summary>
internal sealed class WebhookDeliveryDisplayDriver : DisplayDriver<WebhookDelivery>
{
    public override IDisplayResult Display(WebhookDelivery delivery, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = delivery.EventName ?? string.Empty;
        }

        return Combine(
            View("WebhookDelivery_Fields_SummaryAdmin", delivery)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Event:1"),
            View("WebhookDelivery_Description_SummaryAdmin", delivery)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Event:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(delivery.LastError))),
            View("WebhookDelivery_Status_SummaryAdmin", delivery)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("WebhookDelivery_Meta_SummaryAdmin", delivery)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Sent:1"),
            View("WebhookDelivery_Buttons_SummaryAdmin", delivery).Location("Actions:5"));
    }
}
