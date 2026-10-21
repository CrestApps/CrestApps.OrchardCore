using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// The disputes against an order's payment: under the items on the store's order page, and as a badge in the
/// orders list while one is open.
/// </summary>
internal sealed class OrderDisputesDisplayDriver : DisplayDriver<Order>
{
    public override IDisplayResult Display(Order order, BuildDisplayContext context)
    {
        if (order.Disputes is not { Count: > 0 })
        {
            return null;
        }

        if (context.DisplayType == OrderConstants.DisplayTypes.DetailAdmin)
        {
            return View("OrderDisputes_DetailAdmin", order).Location("Content:0");
        }

        if (order.HasOpenDispute && context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return View("Order_Dispute_SummaryAdmin", order)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:2")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:2");
        }

        return null;
    }
}
