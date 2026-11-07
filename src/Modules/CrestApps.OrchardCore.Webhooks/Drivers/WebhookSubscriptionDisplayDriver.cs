using CrestApps.OrchardCore.Webhooks.Core.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Webhooks.Drivers;

/// <summary>
/// Renders a webhook subscription in the admin list, in both the List and the Grid layout.
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
