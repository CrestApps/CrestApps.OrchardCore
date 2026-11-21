using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Builds the rows of the survey template list in both admin list layouts.
/// </summary>
internal sealed class SurveyTemplateDisplayDriver : DisplayDriver<SurveyTemplate>
{
    /// <inheritdoc />
    public override IDisplayResult Display(SurveyTemplate template, BuildDisplayContext context)
    {
        if (context.DisplayType is not OrchardCoreConstants.DisplayType.SummaryAdmin and not OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{template.Title} {template.Name}".ToLowerInvariant();
        }

        return Combine(
            View("SurveyTemplate_Fields_SummaryAdmin", template)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("SurveyTemplate_Description_SummaryAdmin", template)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("SurveyTemplate_Tags_SummaryAdmin", template)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Kind:5"),
            View("SurveyTemplate_Meta_SummaryAdmin", template)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5"),
            View("SurveyTemplate_Buttons_SummaryAdmin", template).Location("Actions:5"),
            View("SurveyTemplate_ActionsMenu_SummaryAdmin", template).Location("ActionsMenu:10"));
    }
}
