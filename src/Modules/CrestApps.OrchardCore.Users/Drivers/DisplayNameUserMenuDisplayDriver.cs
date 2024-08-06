using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Drivers;

public sealed class DisplayNameUserMenuDisplayDriver : DisplayDriver<UserMenu>
{
    public override IDisplayResult Display(UserMenu model, BuildDisplayContext buildDisplayContext)
    {
        return View("UserMenuItems__DisplayName", model)
            .Location("Detail", "Header:4")
            .Location("DetailAdmin", "Header:4")
            .Differentiator("DisplayName");
    }
}
