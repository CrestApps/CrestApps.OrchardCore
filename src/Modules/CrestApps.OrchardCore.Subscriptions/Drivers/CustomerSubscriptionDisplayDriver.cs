using CrestApps.OrchardCore.Subscriptions.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Builds the row of a subscription in its owner's own plans (My Plans), for both admin list layouts.
/// </summary>
public sealed class CustomerSubscriptionDisplayDriver : DisplayDriver<CustomerSubscription>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(CustomerSubscription entry, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = entry.Subscription.Title?.ToLowerInvariant() ?? string.Empty;
        }

        var results = new List<IDisplayResult>
        {
            View("CustomerSubscription_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Title:1"),
            View("CustomerSubscription_Status_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("CustomerSubscription_Amount_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Amount:5"),
            View("CustomerSubscription_Renews_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Renews:5"),
            View("CustomerSubscription_AccessUntil_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:15")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "AccessUntil:5"),
        };

        // Cancelling is self-service, for as long as the agreement can still take a payment.
        if (entry.Subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.PastDue)
        {
            results.Add(View("CustomerSubscription_Cancel_SummaryAdmin", entry).Location("Actions:5"));
        }

        return CombineAsync(results);
    }
}
