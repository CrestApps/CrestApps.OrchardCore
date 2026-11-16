using CrestApps.OrchardCore.Learning.Coursework.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Coursework.Drivers;

/// <summary>
/// The row of a submission on the submissions page of a coursework item.
/// </summary>
internal sealed class SubmissionRowDisplayDriver : DisplayDriver<SubmissionRow>
{
    public override Task<IDisplayResult> DisplayAsync(SubmissionRow row, BuildDisplayContext context)
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
            View("SubmissionRow_Fields_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Learner:1"),
            View("SubmissionRow_Submitted_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Submitted:1"),
            View("SubmissionRow_Status_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("SubmissionRow_Buttons_SummaryAdmin", row).Location("Actions:5"));
    }
}
