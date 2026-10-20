using System.Security.Claims;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// Renders an order as a row of the orders admin list and of the signed-in customer's own orders, in both of
/// their layouts (List and Grid).
/// </summary>
internal sealed class OrderDisplayDriver : DisplayDriver<Order>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OrderDisplayDriver(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task<IDisplayResult> DisplayAsync(Order order, BuildDisplayContext context)
    {
        // An order is only displayed as a row of an admin list; the confirmation and order pages render their own views.
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = order.OrderNumber?.ToLowerInvariant() ?? string.Empty;
        }

        // The customer is left out of the viewer's own orders, e.g. in My Orders: it is the viewer.
        var isOwnOrder = order.UserId is not null
            && order.UserId == _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return CombineAsync(
            View("Order_Fields_SummaryAdmin", order)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("Order_Status_SummaryAdmin", order)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("Order_Channel_SummaryAdmin", order)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5")
                .RenderWhen(() => Task.FromResult(OrderChannels.IsCounterSale(order))),
            View("Order_DefaultMeta_SummaryAdmin", order)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Placed:1"),
            View("Order_Items_SummaryAdmin", order)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:2")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Items:1"),
            View("Order_Total_SummaryAdmin", order)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:3")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Total:1"),
            View("Order_Customer_SummaryAdmin", order)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:4")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Customer:1")
                .RenderWhen(() => Task.FromResult(!isOwnOrder && !string.IsNullOrEmpty(order.CustomerEmail))),
            View("Order_Buttons_SummaryAdmin", order).Location("Actions:5"));
    }
}
