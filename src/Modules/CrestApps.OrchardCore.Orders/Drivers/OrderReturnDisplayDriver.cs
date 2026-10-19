using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// Renders a return as a row of the returns admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class OrderReturnDisplayDriver : DisplayDriver<OrderReturn>
{
    public override IDisplayResult Display(OrderReturn orderReturn, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{orderReturn.Number} {orderReturn.OrderNumber}".ToLowerInvariant();
        }

        void Fill(OrderReturnRowViewModel model)
            => model.Return = orderReturn;

        return Combine(
            Initialize<OrderReturnRowViewModel>("OrderReturn_Fields_SummaryAdmin", Fill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<OrderReturnRowViewModel>("OrderReturn_Description_SummaryAdmin", Fill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            Initialize<OrderReturnRowViewModel>("OrderReturn_Status_SummaryAdmin", Fill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            Initialize<OrderReturnRowViewModel>("OrderReturn_Meta_SummaryAdmin", Fill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Items:1"),
            Initialize<OrderReturnRowViewModel>("OrderReturn_Buttons_SummaryAdmin", Fill).Location("Actions:5"));
    }
}
