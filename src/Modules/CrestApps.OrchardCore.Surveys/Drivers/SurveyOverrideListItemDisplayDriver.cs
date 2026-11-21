using CrestApps.OrchardCore.Surveys.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Renders the rows of the accommodations list in the List and Grid layouts.
/// </summary>
public sealed class SurveyOverrideListItemDisplayDriver : DisplayDriver<SurveyOverrideListItem>
{
    /// <inheritdoc />
    public override Task<IDisplayResult> DisplayAsync(SurveyOverrideListItem model, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        return CombineAsync(
            View("SurveyOverrideListItem_Fields_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("SurveyOverrideListItem_Tags_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Terms:5"),
            View("SurveyOverrideListItem_Meta_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Granted:5"),
            View("SurveyOverrideListItem_Description_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("SurveyOverrideListItem_Buttons_SummaryAdmin", model).Location("Actions:5"));
    }
}
