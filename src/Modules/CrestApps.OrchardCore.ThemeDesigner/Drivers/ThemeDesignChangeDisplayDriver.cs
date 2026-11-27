using CrestApps.OrchardCore.ThemeDesigner.Services;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ThemeDesigner.Drivers;

/// <summary>
/// The row of a change in the theme draft's admin list.
/// </summary>
internal sealed class ThemeDesignChangeDisplayDriver : DisplayDriver<ThemeDesignChange>
{
    public override Task<IDisplayResult> DisplayAsync(ThemeDesignChange change, BuildDisplayContext context)
    {
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = change.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("ThemeDesignChange_Fields_SummaryAdmin", change)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ThemeDesignChange_DefaultTags_SummaryAdmin", change)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Change:5"),
            View("ThemeDesignChange_Buttons_SummaryAdmin", change).Location("Actions:5"),
            View("ThemeDesignChange_DefaultMeta_SummaryAdmin", change)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5")
        );
    }
}
