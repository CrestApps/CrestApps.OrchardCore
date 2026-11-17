using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Commerce.Drivers;

/// <summary>
/// Adds Account, the link to the customer account, to the user menu on the site's pages and in the admin, the way the
/// Users module adds Security, so a signed-in user always sees where their account is.
/// </summary>
public sealed class CommerceAccountUserMenuDisplayDriver : DisplayDriver<UserMenu>
{
    public override IDisplayResult Display(UserMenu model, BuildDisplayContext context)
        => View("UserMenuItems__CommerceAccount", model)
            .Location(OrchardCoreConstants.DisplayType.Detail, "Content:2")
            .Location(OrchardCoreConstants.DisplayType.DetailAdmin, "Content:2")
            .Differentiator("CommerceAccount");
}
