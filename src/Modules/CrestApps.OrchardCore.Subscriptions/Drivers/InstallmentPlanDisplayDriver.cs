using CrestApps.OrchardCore.Subscriptions.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Builds the row of an installment plan in the administration installment plans list, for both admin list layouts.
/// </summary>
public sealed class InstallmentPlanDisplayDriver : DisplayDriver<InstallmentPlan>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(InstallmentPlan plan, BuildDisplayContext context)
    {
        // A plan is only displayed as a row of the list; its detail page is a view of its own.
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = plan.Title?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("InstallmentPlan_Fields_SummaryAdmin", plan)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Title:1"),
            View("InstallmentPlan_Status_SummaryAdmin", plan)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("InstallmentPlan_Customer_SummaryAdmin", plan)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Customer:5"),
            View("InstallmentPlan_Amount_SummaryAdmin", plan)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Amount:5"),
            View("InstallmentPlan_NextPayment_SummaryAdmin", plan)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:15")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "NextPayment:5"),
            View("InstallmentPlan_Buttons_SummaryAdmin", plan).Location("Actions:5"));
    }
}
