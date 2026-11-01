using CrestApps.OrchardCore.Workforce.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// The rows of the PredictabilityPayEntry list.
/// </summary>
internal sealed class PredictabilityPayRowDisplayDriver : DisplayDriver<PredictabilityPayRowViewModel>
{
    public override IDisplayResult Display(PredictabilityPayRowViewModel item, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        return Combine(
            View("PredictabilityPayEntry_Fields_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("PredictabilityPayEntry_Tags_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("PredictabilityPayEntry_Buttons_SummaryAdmin", item).Location("Actions:5"));
    }
}
