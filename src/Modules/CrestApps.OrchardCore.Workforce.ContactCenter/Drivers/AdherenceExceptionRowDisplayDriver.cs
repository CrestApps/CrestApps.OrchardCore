using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Drivers;

/// <summary>
/// Displays an adherence exception in the admin list, in both layouts.
/// </summary>
internal sealed class AdherenceExceptionRowDisplayDriver : DisplayDriver<AdherenceExceptionRow>
{
    /// <inheritdoc/>
    public override IDisplayResult Display(AdherenceExceptionRow item, BuildDisplayContext context)
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
            View("AdherenceExceptionRow_Fields_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AdherenceExceptionRow_Tags_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("AdherenceExceptionRow_Buttons_SummaryAdmin", item).Location("Actions:5"));
    }
}
