using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
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
    private readonly SurveyRespondentNames _names;
    private readonly SurveyResponseRowActions _rowActions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseDisplayDriver"/> class.
    /// </summary>
    /// <param name="names">The respondent names, which also give the label staff see for a response.</param>
    /// <param name="rowActions">The responses the user may void and delete, filled by the list.</param>
    public SurveyResponseDisplayDriver(SurveyRespondentNames names, SurveyResponseRowActions rowActions)
    {
        _names = names;
        _rowActions = rowActions;
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> DisplayAsync(SurveyResponse response, BuildDisplayContext context)
    {
        if (context.DisplayType is not OrchardCoreConstants.DisplayType.SummaryAdmin and not OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            // An anonymous response is filtered by the label it shows, never by its site-wide number.
            rowShape.Attributes["data-filter-value"] = (await _names.GetLabelAsync(response)).ToLowerInvariant();
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
            // Void and Delete show only on a response the user may remove (not their own, and a given grade only for
            // whoever may change scores), so nobody is offered an action that ends on the access denied page.
            View("SurveyResponse_ActionsMenu_SummaryAdmin", response)
                .Location("ActionsMenu:10")
                .RenderWhen(() => Task.FromResult(_rowActions.RemovableIds.Contains(response.ItemId))));
    }
}
