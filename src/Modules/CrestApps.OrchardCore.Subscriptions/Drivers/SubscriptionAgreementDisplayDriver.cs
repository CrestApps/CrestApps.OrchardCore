using CrestApps.OrchardCore.Subscriptions.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Builds the row of a subscription agreement in the administration agreements report, for both admin list layouts.
/// </summary>
public sealed class SubscriptionAgreementDisplayDriver : DisplayDriver<SubscriptionAgreement>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(SubscriptionAgreement agreement, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = agreement.Subscription.Title?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("SubscriptionAgreement_Fields_SummaryAdmin", agreement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Title:1"),
            View("SubscriptionAgreement_Status_SummaryAdmin", agreement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("SubscriptionAgreement_Method_SummaryAdmin", agreement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Method:5"),
            View("SubscriptionAgreement_Amount_SummaryAdmin", agreement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Amount:5"),
            View("SubscriptionAgreement_Renews_SummaryAdmin", agreement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:15")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Renews:5"),
            View("SubscriptionAgreement_PaidThrough_SummaryAdmin", agreement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:20")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "PaidThrough:5"),
            View("SubscriptionAgreement_Buttons_SummaryAdmin", agreement).Location("Actions:5"));
    }
}
