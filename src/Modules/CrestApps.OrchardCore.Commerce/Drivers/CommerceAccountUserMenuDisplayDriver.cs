using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Commerce.Drivers;

/// <summary>
/// Adds a link to the storefront account to the user menu on the site's pages.
/// </summary>
public sealed class CommerceAccountUserMenuDisplayDriver : DisplayDriver<UserMenu>
{
    public override IDisplayResult Display(UserMenu model, BuildDisplayContext context)
        => View("UserMenuItems__CommerceAccount", model)
            .Location("Detail", "Content:2")
            .Differentiator("CommerceAccount");
}
