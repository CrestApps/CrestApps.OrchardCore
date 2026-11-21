using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Renders a collector as a row of the collectors list.
/// </summary>
internal sealed class SurveyCollectorDisplayDriver : DisplayDriver<SurveyCollector>
{
    /// <inheritdoc />
    public override IDisplayResult Display(SurveyCollector collector, BuildDisplayContext context)
    {
        if (context.DisplayType is not OrchardCoreConstants.DisplayType.SummaryAdmin and not OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return Combine(
            View("SurveyCollector_Fields_SummaryAdmin", collector)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("SurveyCollector_Tags_SummaryAdmin", collector)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("SurveyCollector_Description_SummaryAdmin", collector)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Link:5"),
            View("SurveyCollector_Buttons_SummaryAdmin", collector).Location("Actions:5"),
            View("SurveyCollector_ActionsMenu_SummaryAdmin", collector).Location("ActionsMenu:10"));
    }
}
