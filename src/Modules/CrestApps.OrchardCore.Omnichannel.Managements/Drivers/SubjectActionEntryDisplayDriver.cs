using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Builds the row of a subject action on the page that manages the flow of a subject.
/// </summary>
internal sealed class SubjectActionEntryDisplayDriver : DisplayDriver<SubjectActionEntry>
{
    public override Task<IDisplayResult> DisplayAsync(SubjectActionEntry entry, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{entry.DispositionDisplayText} {entry.ActionTypeDisplayName}".ToLowerInvariant();
        }

        return CombineAsync(
            View("SubjectActionEntry_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Disposition:1"),
            View("SubjectActionEntry_DefaultTags_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Type:5"),
            View("SubjectActionEntry_Buttons_SummaryAdmin", entry).Location("Actions:5"),
            View("SubjectActionEntry_ActionsMenu_SummaryAdmin", entry).Location("ActionsMenu:10"));
    }
}
