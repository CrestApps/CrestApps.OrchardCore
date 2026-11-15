using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Drivers;

/// <summary>
/// Adds a link to the "my account" area to the user menu on the site's pages.
/// </summary>
public sealed class AccountUserMenuDisplayDriver : DisplayDriver<UserMenu>
{
    public override IDisplayResult Display(UserMenu model, BuildDisplayContext context)
        => View("UserMenuItems__CommerceAccount", model)
            .Location("Detail", "Content:2")
            .Differentiator("CommerceAccount");
}
