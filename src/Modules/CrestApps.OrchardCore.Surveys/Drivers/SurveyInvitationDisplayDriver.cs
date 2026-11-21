using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Shows an invitation as a row of the invitations list.
/// </summary>
internal sealed class SurveyInvitationDisplayDriver : DisplayDriver<SurveyInvitation>
{
    /// <inheritdoc />
    public override Task<IDisplayResult> DisplayAsync(SurveyInvitation invitation, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return CombineAsync(
            View("SurveyInvitation_Fields_SummaryAdmin", invitation)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Recipient:1"),
            View("SurveyInvitation_Status_SummaryAdmin", invitation)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("SurveyInvitation_Meta_SummaryAdmin", invitation)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Sent:5"));
    }
}
