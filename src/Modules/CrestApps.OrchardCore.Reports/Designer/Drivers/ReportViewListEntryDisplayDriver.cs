using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Reports.Designer.Drivers;

/// <summary>
/// Renders a view's row on the Report Views list: its name, its refresh schedule, its description, its columns and
/// designer, how its stored result was last refreshed, and what the user may do with it.
/// </summary>
internal sealed class ReportViewListEntryDisplayDriver : DisplayDriver<ReportViewListEntry>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(ReportViewListEntry entry, BuildDisplayContext context)
    {
        // The row's classes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("ReportViewListEntry_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ReportViewListEntry_Description_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("ReportViewListEntry_Tags_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Refresh:5"),
            View("ReportViewListEntry_Meta_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("ReportViewListEntry_Buttons_SummaryAdmin", entry).Location("Actions:5"),
            View("ReportViewListEntry_ActionsMenu_SummaryAdmin", entry).Location("ActionsMenu:10"));
    }
}
