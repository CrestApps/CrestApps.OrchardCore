using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Users.Cohorts;

/// <summary>
/// The row of a cohort in the cohorts list.
/// </summary>
internal sealed class CohortRowDisplayDriver : DisplayDriver<CohortRow>
{
    public override Task<IDisplayResult> DisplayAsync(CohortRow row, BuildDisplayContext context)
        => CombineAsync(
            View("CohortRow_Fields_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("CohortRow_Tags_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Members:5"),
            View("CohortRow_Buttons_SummaryAdmin", row).Location("Actions:5"),
            View("CohortRow_ActionsMenu_SummaryAdmin", row).Location("ActionsMenu:10"));
}

/// <summary>
/// The row of a person in the members of a cohort.
/// </summary>
internal sealed class CohortMemberRowDisplayDriver : DisplayDriver<CohortMemberRow>
{
    public override Task<IDisplayResult> DisplayAsync(CohortMemberRow row, BuildDisplayContext context)
        => CombineAsync(
            View("CohortMemberRow_Fields_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("CohortMemberRow_Email_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Email:5"),
            View("CohortMemberRow_Added_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Added:10"),
            View("CohortMemberRow_Buttons_SummaryAdmin", row).Location("Actions:5"));
}
