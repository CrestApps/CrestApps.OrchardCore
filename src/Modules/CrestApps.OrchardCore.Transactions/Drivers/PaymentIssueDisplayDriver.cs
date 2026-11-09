using CrestApps.OrchardCore.Transactions.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Transactions.Drivers;

/// <summary>
/// Builds the row of a payment issue in the administration list, for both admin list layouts.
/// </summary>
public sealed class PaymentIssueDisplayDriver : DisplayDriver<PaymentIssue>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(PaymentIssue issue, BuildDisplayContext context)
    {
        // An issue is only displayed as a row of the list.
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = issue.Message?.ToLowerInvariant() ?? string.Empty;
        }

        var results = new List<IDisplayResult>
        {
            View("PaymentIssue_Fields_SummaryAdmin", issue)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Issue:1"),
            View("PaymentIssue_Description_SummaryAdmin", issue)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Issue:5"),
            View("PaymentIssue_Status_SummaryAdmin", issue)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("PaymentIssue_Meta_SummaryAdmin", issue)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
        };

        // Only an open issue needs an operator to record that they finished it.
        if (issue.Status == PaymentIssueStatus.Open)
        {
            results.Add(View("PaymentIssue_Resolve_SummaryAdmin", issue).Location("Actions:5"));
        }

        return CombineAsync(results);
    }
}
