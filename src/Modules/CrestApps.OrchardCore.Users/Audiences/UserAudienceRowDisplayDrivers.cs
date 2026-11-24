using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Users.Audiences;

/// <summary>
/// The row of an audience in the audiences list.
/// </summary>
internal sealed class UserAudienceRowDisplayDriver : DisplayDriver<UserAudienceRow>
{
    public override Task<IDisplayResult> DisplayAsync(UserAudienceRow row, BuildDisplayContext context)
    {
        var results = new List<IDisplayResult>
        {
            View("UserAudienceRow_Fields_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("UserAudienceRow_Tags_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("UserAudienceRow_Buttons_SummaryAdmin", row).Location("Actions:5"),
        };

        // An audience a feature defines has nothing to delete, so it gets no menu.
        if (row.IsDefined)
        {
            results.Add(View("UserAudienceRow_ActionsMenu_SummaryAdmin", row).Location("ActionsMenu:10"));
        }

        return CombineAsync(results);
    }
}

/// <summary>
/// The row of a person in the members of an audience.
/// </summary>
internal sealed class UserAudienceMemberRowDisplayDriver : DisplayDriver<UserAudienceMemberRow>
{
    public override Task<IDisplayResult> DisplayAsync(UserAudienceMemberRow row, BuildDisplayContext context)
        => CombineAsync(
            View("UserAudienceMemberRow_Fields_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("UserAudienceMemberRow_Email_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Email:5"));
}
