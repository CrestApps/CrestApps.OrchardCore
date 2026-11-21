using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Renders a survey as a row of the survey list.
/// </summary>
internal sealed class SurveyDisplayDriver : DisplayDriver<Survey>
{
    /// <inheritdoc />
    public override IDisplayResult Display(Survey survey, BuildDisplayContext context)
    {
        if (context.DisplayType is not OrchardCoreConstants.DisplayType.SummaryAdmin and not OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{survey.Title} {survey.Name}".ToLowerInvariant();
        }

        return Combine(
            View("Survey_Fields_SummaryAdmin", survey)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("Survey_Tags_SummaryAdmin", survey)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("Survey_Meta_SummaryAdmin", survey)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5"),
            View("Survey_Description_SummaryAdmin", survey)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("Survey_Buttons_SummaryAdmin", survey).Location("Actions:5"),
            View("Survey_ActionsMenu_SummaryAdmin", survey).Location("ActionsMenu:10"));
    }
}
