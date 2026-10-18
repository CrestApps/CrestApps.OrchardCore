using CrestApps.OrchardCore.Transactions.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Transactions.Drivers;

/// <summary>
/// Builds the row of a transaction in its owner's statement (My Transactions), for both admin list layouts.
/// </summary>
public sealed class CustomerTransactionDisplayDriver : DisplayDriver<CustomerTransaction>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(CustomerTransaction entry, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = entry.Transaction.Title?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("CustomerTransaction_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Title:1"),
            View("CustomerTransaction_Status_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("CustomerTransaction_Amount_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Amount:5"),
            View("CustomerTransaction_Due_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Due:5"),
            View("CustomerTransaction_Buttons_SummaryAdmin", entry).Location("Actions:5"));
    }
}
