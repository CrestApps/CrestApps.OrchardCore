using CrestApps.OrchardCore.Skills.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Skills.Drivers;

/// <summary>
/// The rows of the certification list.
/// </summary>
internal sealed class CertificationRowDisplayDriver : DisplayDriver<Certification>
{
    public override IDisplayResult Display(Certification certification, BuildDisplayContext context)
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
            View("Certification_Fields_SummaryAdmin", certification)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("Certification_Tags_SummaryAdmin", certification)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("Certification_Buttons_SummaryAdmin", certification).Location("Actions:5"),
            View("Certification_ActionsMenu_SummaryAdmin", certification).Location("ActionsMenu:10"));
    }
}
