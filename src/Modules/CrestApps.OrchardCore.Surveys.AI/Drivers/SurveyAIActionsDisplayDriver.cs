using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.AI.Drivers;

/// <summary>
/// Adds the AI actions (summarize answers, translate with AI) to each survey's row in the survey list.
/// </summary>
internal sealed class SurveyAIActionsDisplayDriver : DisplayDriver<Survey>
{
    /// <inheritdoc />
    public override IDisplayResult Display(Survey survey, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        return View("Survey_AIActions_SummaryAdmin", survey).Location("ActionsMenu:20");
    }
}
