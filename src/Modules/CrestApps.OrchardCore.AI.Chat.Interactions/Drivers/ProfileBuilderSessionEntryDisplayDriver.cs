using CrestApps.OrchardCore.AI.Chat.Interactions.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

/// <summary>
/// Display driver for a row of the AI Profile Builder session list.
/// </summary>
internal sealed class ProfileBuilderSessionEntryDisplayDriver : DisplayDriver<ProfileBuilderSessionEntry>
{
    public override IDisplayResult Display(ProfileBuilderSessionEntry entry, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = entry.Title?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("ProfileBuilderSessionEntry_Checkbox_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1"),
            View("ProfileBuilderSessionEntry_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ProfileBuilderSessionEntry_Buttons_SummaryAdmin", entry).Location("Actions:5"),
            View("ProfileBuilderSessionEntry_DefaultTags_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Activity:5"),
            View("ProfileBuilderSessionEntry_DefaultMeta_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5"),
            View("ProfileBuilderSessionEntry_ActionsMenu_SummaryAdmin", entry).Location("ActionsMenu:10")
        );
    }
}
