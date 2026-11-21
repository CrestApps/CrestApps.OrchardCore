using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// The row of a response in the responses list, in the List and Grid layouts.
/// </summary>
public sealed class SurveyResponseDisplayDriver : DisplayDriver<SurveyResponse>
{
    /// <inheritdoc />
    public override IDisplayResult Display(SurveyResponse response, BuildDisplayContext context)
    {
        if (context.DisplayType is not OrchardCoreConstants.DisplayType.SummaryAdmin and not OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = response.Number?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("SurveyResponse_Checkbox_SummaryAdmin", response)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1"),
            View("SurveyResponse_Fields_SummaryAdmin", response)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Number:1"),
            View("SurveyResponse_Status_SummaryAdmin", response)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("SurveyResponse_Meta_SummaryAdmin", response)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Started:5"),
            View("SurveyResponse_Buttons_SummaryAdmin", response).Location("Actions:5"),
            View("SurveyResponse_ActionsMenu_SummaryAdmin", response).Location("ActionsMenu:10"));
    }
}
