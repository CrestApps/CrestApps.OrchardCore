using CrestApps.OrchardCore.Transactions.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Transactions.Drivers;

/// <summary>
/// Builds the row of a refund in the administration refunds ledger, for both admin list layouts.
/// </summary>
public sealed class PaymentRefundDisplayDriver : DisplayDriver<PaymentRefund>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(PaymentRefund refund, BuildDisplayContext context)
    {
        // A refund is only displayed as a row of the ledger.
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = (refund.FailureReason ?? refund.Reason)?.ToLowerInvariant() ?? string.Empty;
        }

        var results = new List<IDisplayResult>
        {
            View("PaymentRefund_Fields_SummaryAdmin", refund)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Amount:1"),
            View("PaymentRefund_Description_SummaryAdmin", refund)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Amount:5"),
            View("PaymentRefund_Status_SummaryAdmin", refund)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("PaymentRefund_Method_SummaryAdmin", refund)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Method:5"),
            View("PaymentRefund_Updated_SummaryAdmin", refund)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Updated:5"),
            View("PaymentRefund_Tax_SummaryAdmin", refund)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:15")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Tax:5"),
        };

        // Only a refund the provider could not settle on its own needs an operator to record that they did.
        if (refund.Status == RefundStatus.PendingManualReview)
        {
            results.Add(View("PaymentRefund_Resolve_SummaryAdmin", refund).Location("Actions:5"));
        }

        return CombineAsync(results);
    }
}
