using CrestApps.OrchardCore.Accounts.Models;
using OrchardCore;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Accounts.Drivers;

/// <summary>
/// The Notes tab of the account page: the account's timeline, with a composer for the team's notes.
/// </summary>
internal sealed class AccountNotesDisplayDriver : ContentPartDisplayDriver<AccountPart>
{
    public override IDisplayResult Display(AccountPart part, BuildPartDisplayContext context)
    {
        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin)
        {
            return null;
        }

        return View("AccountNotesTab", part)
            .Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Notes));
    }
}
