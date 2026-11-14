using CrestApps.OrchardCore.AdminTheme.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AdminTheme.Drivers;

/// <summary>
/// The rows of the list of color schemes admins added, in both admin list layouts.
/// </summary>
public sealed class AdminColorSchemeDisplayDriver : DisplayDriver<AdminColorScheme>
{
    public override IDisplayResult Display(AdminColorScheme scheme, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
            row.Attributes["data-filter-value"] = scheme.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("AdminColorScheme_Fields_SummaryAdmin", scheme)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AdminColorScheme_Swatch_SummaryAdmin", scheme)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Colors:5"),
            View("AdminColorScheme_Buttons_SummaryAdmin", scheme).Location("Actions:5"),
            View("AdminColorScheme_ActionsMenu_SummaryAdmin", scheme).Location("ActionsMenu:10"));
    }
}
