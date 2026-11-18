using CrestApps.OrchardCore.Approvals.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Approvals.Drivers;

/// <summary>
/// The rows of the list of approval requests.
/// </summary>
internal sealed class ApprovalRequestDisplayDriver : DisplayDriver<ApprovalRequest>
{
    public override Task<IDisplayResult> DisplayAsync(ApprovalRequest request, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("ApprovalRequest_Fields_SummaryAdmin", request)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Request:1"),
            View("ApprovalRequest_Description_SummaryAdmin", request)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("ApprovalRequest_Tags_SummaryAdmin", request)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("ApprovalRequest_Buttons_SummaryAdmin", request).Location("Actions:5"));
    }
}
