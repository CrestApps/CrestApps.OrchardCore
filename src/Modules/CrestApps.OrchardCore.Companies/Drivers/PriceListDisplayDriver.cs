using CrestApps.OrchardCore.Companies.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Companies.Drivers;

/// <summary>
/// Renders a price list as a row of the price lists admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class PriceListDisplayDriver : DisplayDriver<PriceList>
{
    public override IDisplayResult Display(PriceList list, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = list.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("PriceList_Fields_SummaryAdmin", list)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("PriceList_Description_SummaryAdmin", list)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Prices:1"),
            View("PriceList_Buttons_SummaryAdmin", list).Location("Actions:5"));
    }
}
