using CrestApps.OrchardCore.Surveys.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Renders the rows of the grading queue in the List and Grid layouts.
/// </summary>
public sealed class GradingQueueItemDisplayDriver : DisplayDriver<GradingQueueItem>
{
    /// <inheritdoc />
    public override Task<IDisplayResult> DisplayAsync(GradingQueueItem model, BuildDisplayContext context)
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
            View("GradingQueueItem_Fields_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("GradingQueueItem_Tags_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Survey:5"),
            View("GradingQueueItem_Meta_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Completed:5"),
            View("GradingQueueItem_Buttons_SummaryAdmin", model).Location("Actions:5"));
    }
}
