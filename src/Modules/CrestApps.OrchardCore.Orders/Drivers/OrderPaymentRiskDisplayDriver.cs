using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// A payment the gateway's fraud checks rated risky: a warning on the store's order page, and a badge in the orders
/// list.
/// </summary>
internal sealed class OrderPaymentRiskDisplayDriver : DisplayDriver<Order>
{
    public override IDisplayResult Display(Order order, BuildDisplayContext context)
    {
        if (order.PaymentRisk is not { } risk)
        {
            return null;
        }

        if (context.DisplayType == OrderConstants.DisplayTypes.DetailAdmin)
        {
            return View("OrderPaymentRisk_DetailAdmin", order).Location("Content:0");
        }

        if (risk.IsElevated && context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return View("Order_PaymentRisk_SummaryAdmin", order)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:3")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:3");
        }

        return null;
    }
}
