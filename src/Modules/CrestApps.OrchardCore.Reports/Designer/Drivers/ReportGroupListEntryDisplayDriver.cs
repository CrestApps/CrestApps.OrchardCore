using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Reports.Designer.Drivers;

/// <summary>
/// Renders a report group's row on the report group list: its path, how many groups and reports it holds, and its
/// actions.
/// </summary>
internal sealed class ReportGroupListEntryDisplayDriver : DisplayDriver<ReportGroupListEntry>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(ReportGroupListEntry entry, BuildDisplayContext context)
    {
        // The row's classes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("ReportGroupListEntry_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ReportGroupListEntry_Tags_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Contents:5"),
            View("ReportGroupListEntry_Buttons_SummaryAdmin", entry).Location("Actions:5"),
            View("ReportGroupListEntry_ActionsMenu_SummaryAdmin", entry).Location("ActionsMenu:10"));
    }
}
