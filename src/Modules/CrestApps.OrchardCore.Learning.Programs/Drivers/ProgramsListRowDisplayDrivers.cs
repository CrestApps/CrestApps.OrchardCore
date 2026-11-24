using CrestApps.OrchardCore.Learning.Programs.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Programs.Drivers;

/// <summary>
/// The row of a person on the team learning page.
/// </summary>
internal sealed class TeamLearningRowDisplayDriver : DisplayDriver<TeamLearningRow>
{
    public override Task<IDisplayResult> DisplayAsync(TeamLearningRow row, BuildDisplayContext context)
        => CombineAsync(
            View("TeamLearningRow_Fields_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("TeamLearningRow_Learning_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Learning:5"));
}

/// <summary>
/// The row of a person on the preview of a rule.
/// </summary>
internal sealed class RequiredLearningPreviewRowDisplayDriver : DisplayDriver<RequiredLearningPreviewRow>
{
    public override Task<IDisplayResult> DisplayAsync(RequiredLearningPreviewRow row, BuildDisplayContext context)
        => CombineAsync(
            View("RequiredLearningPreviewRow_Fields_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("RequiredLearningPreviewRow_State_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "State:5"));
}
