using CrestApps.OrchardCore.MailingLists.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.MailingLists.Drivers;

/// <summary>
/// The row of a member on its list's members page, in both admin list layouts.
/// </summary>
internal sealed class MailingListMemberDisplayDriver : DisplayDriver<MailingListMember>
{
    public override IDisplayResult Display(MailingListMember member, BuildDisplayContext context)
    {
        if (context.DisplayType is not OrchardCoreConstants.DisplayType.SummaryAdmin and not OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = member.NormalizedEmail ?? string.Empty;
        }

        return Combine(
            View("MailingListMember_Checkbox_SummaryAdmin", member)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1"),
            View("MailingListMember_Fields_SummaryAdmin", member)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Email:1"),
            View("MailingListMember_Status_SummaryAdmin", member)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("MailingListMember_Meta_SummaryAdmin", member)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Changed:5"),
            View("MailingListMember_Buttons_SummaryAdmin", member).Location("Actions:5"),
            View("MailingListMember_ActionsMenu_SummaryAdmin", member).Location("ActionsMenu:10"));
    }
}
