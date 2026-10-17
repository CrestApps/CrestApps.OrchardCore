using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Reports.Designer.Drivers;

/// <summary>
/// Renders a designed report's row on the Report Builder list: its title, whether it is published, its category, menu
/// and sharing badges, its description, who designed it, and what the user may do with it.
/// </summary>
internal sealed class ReportDesignListEntryDisplayDriver : DisplayDriver<ReportDesignListEntry>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(ReportDesignListEntry entry, BuildDisplayContext context)
    {
        // The row's classes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("ReportDesignListEntry_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ReportDesignListEntry_Description_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("ReportDesignListEntry_Tags_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("ReportDesignListEntry_Meta_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Author:5"),
            View("ReportDesignListEntry_Buttons_SummaryAdmin", entry).Location("Actions:5"),
            View("ReportDesignListEntry_ActionsMenu_SummaryAdmin", entry).Location("ActionsMenu:10"));
    }
}
