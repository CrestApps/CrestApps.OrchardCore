using CrestApps.OrchardCore.AI.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Display driver for a row of an AI profile's versions list.
/// </summary>
internal sealed class AIProfileVersionEntryDisplayDriver : DisplayDriver<AIProfileVersionEntry>
{
    public override IDisplayResult Display(AIProfileVersionEntry entry, BuildDisplayContext context)
    {
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return Combine(
            View("AIProfileVersionEntry_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AIProfileVersionEntry_DefaultTags_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:2"),
            View("AIProfileVersionEntry_DefaultMeta_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Saved:5"),
            View("AIProfileVersionEntry_Description_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Note:5"),
            View("AIProfileVersionEntry_Buttons_SummaryAdmin", entry).Location("Actions:5"),
            View("AIProfileVersionEntry_ActionsMenu_SummaryAdmin", entry).Location("ActionsMenu:10")
        );
    }
}
