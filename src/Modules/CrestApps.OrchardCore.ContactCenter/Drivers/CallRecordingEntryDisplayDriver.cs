using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

/// <summary>
/// Renders a recorded call's row on the call recordings list: who the call was with, its direction and kind, when it
/// started, the agent on it, its length, and the buttons that play and open it.
/// </summary>
internal sealed class CallRecordingEntryDisplayDriver : DisplayDriver<CallRecordingEntry>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(CallRecordingEntry entry, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = entry.CustomerNumber?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("CallRecordingEntry_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("CallRecordingEntry_DefaultTags_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Call:5"),
            View("CallRecordingEntry_Started_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Started:5"),
            View("CallRecordingEntry_Agent_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Agent:5"),
            View("CallRecordingEntry_Length_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:15")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Length:5"),
            View("CallRecordingEntry_Buttons_SummaryAdmin", entry).Location("Actions:5")
        );
    }
}
