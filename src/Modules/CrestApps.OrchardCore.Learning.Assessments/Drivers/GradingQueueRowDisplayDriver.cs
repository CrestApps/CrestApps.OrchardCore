using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

/// <summary>
/// The row of a piece of work on the grading list, whichever source it came from.
/// </summary>
internal sealed class GradingQueueRowDisplayDriver : DisplayDriver<GradingQueueRow>
{
    public override Task<IDisplayResult> DisplayAsync(GradingQueueRow row, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape shape)
        {
            shape.Classes.Add("item");
        }

        return CombineAsync(
            View("GradingQueueRow_Fields_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Learner:1"),
            View("GradingQueueRow_Item_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Item:1"),
            View("GradingQueueRow_Submitted_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Submitted:5"),
            View("GradingQueueRow_Buttons_SummaryAdmin", row).Location("Actions:5"),
            View("GradingQueueRow_ActionsMenu_SummaryAdmin", row).Location("ActionsMenu:10"));
    }
}
